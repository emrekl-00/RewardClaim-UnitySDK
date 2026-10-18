using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RewardClaims.Tests
{
    // Case requirement: "A pending claim should not prevent the player from spending an existing
    // balance." Every test holds a coins claim open (the request is on the wire), spends while it is
    // open, then answers it. The wallet never looks at claims: the balance is the player's, and a
    // pending reward isn't theirs until the server accepts it.
    public class SpendWhileClaimPendingTests
    {
        private const string RewardId = "level-10-first-completion";

        private static readonly ResourceDefinition Coins = ResourceDefinition.Create("coins", startingBalance: 100);
        private static readonly ResourceDefinition Lives = ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5);

        private InMemoryStorage storage;
        private FakeRewardApi api;
        private ResourceWallet wallet;
        private ClaimManager manager;
        private List<(string resourceId, int balance)> balanceEvents;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemoryStorage();
            api = new FakeRewardApi { HoldRequests = true };
            var store = new ProgressStore(storage, TestWallets.PlayerId);
            wallet = new ResourceWallet(new[] { Coins, Lives }, store);
            manager = new ClaimManager(store, wallet, api, TestWallets.PlayerId);

            balanceEvents = new List<(string, int)>();
            wallet.BalanceChanged += (resourceId, balance) => balanceEvents.Add((resourceId, balance));
        }

        [TearDown]
        public void TearDown()
        {
            // Every test answers its own held request; this only matters when a test failed half-way.
            api.AnswerAllHeld(ApiResult.Failure(ClaimError.Timeout));
        }

        // Sends a 50 coins claim whose request stays open until the test answers it.
        private Task<ClaimRecord> StartPendingCoinsClaim()
        {
            var claim = manager.ClaimAsync(RewardId, "coins", 50);
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(api.HeldCount, Is.EqualTo(1));
            return claim;
        }

        // --- Spending works while the request is on the wire ---

        [Test]
        public async Task TrySpend_WhileClaimPending_SpendsRightAwayAndLeavesClaimPending()
        {
            var claim = StartPendingCoinsClaim();

            var spent = wallet.TrySpend("coins", 30);

            // Synchronous: the spend didn't wait for the request, which is still open.
            Assert.That(spent, Is.True);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(70));
            Assert.That(balanceEvents, Is.EqualTo(new[] { ("coins", 70) }));
            Assert.That(claim.IsCompleted, Is.False);
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Pending));

            api.AnswerHeld(ApiResult.Success());
            await claim;
        }

        [Test]
        public async Task ClaimSucceeds_AfterSpendWhilePending_AddsRewardOnTopOfReducedBalanceOnce()
        {
            var claim = StartPendingCoinsClaim();
            wallet.TrySpend("coins", 30);

            api.AnswerHeld(ApiResult.Success());
            var record = await claim;

            // 100 - 30 = 70, then + 50 = 120: the grant adds to the balance as it is now, not as it
            // was when the claim was sent.
            Assert.That(record.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(120));
            Assert.That(balanceEvents, Is.EqualTo(new[] { ("coins", 70), ("coins", 120) }));

            // Granted once: claiming again neither sends nor adds anything.
            await manager.ClaimAsync(RewardId, "coins", 50);
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(120));
        }

        [Test]
        public async Task ClaimFails_AfterSpendingWholeBalanceWhilePending_NoRollbackAndNoGrant()
        {
            var claim = StartPendingCoinsClaim();
            Assert.That(wallet.TrySpend("coins", 100), Is.True);

            api.AnswerHeld(ApiResult.Failure(ClaimError.NoConnection));
            var record = await claim;

            // The spend was the player's own decision, so a failed claim doesn't undo it; and the
            // reward was never theirs, so nothing is granted.
            Assert.That(record.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(record.LastError, Is.EqualTo(ClaimError.NoConnection));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(0));
            Assert.That(balanceEvents, Is.EqualTo(new[] { ("coins", 0) }));

            // Still retryable: the next attempt grants it on top of the empty balance.
            var retry = manager.ClaimAsync(RewardId, "coins", 50);
            api.AnswerHeld(ApiResult.Success());
            await retry;

            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(50));
        }

        // --- Balances and claims share one document ---

        [Test]
        public async Task TrySpend_WhileClaimPending_SavesNewBalanceWithoutOverwritingClaim()
        {
            var claim = StartPendingCoinsClaim();

            wallet.TrySpend("coins", 30);

            // The raw JSON the next launch would read: the spend saved the whole document, so the
            // Pending claim (status 0) is still in it next to the new balance.
            var json = storage.Load(TestWallets.ProgressKey);
            Assert.That(json, Does.Contain("{\"resourceId\":\"coins\",\"balance\":70}"));
            Assert.That(json, Does.Contain($"\"rewardId\":\"{RewardId}\""));
            Assert.That(json, Does.Contain("\"status\":0"));

            // And the grant saves the other way round without losing the spend.
            api.AnswerHeld(ApiResult.Success());
            await claim;

            json = storage.Load(TestWallets.ProgressKey);
            Assert.That(json, Does.Contain("{\"resourceId\":\"coins\",\"balance\":120}"));
            Assert.That(json, Does.Contain("\"status\":1"));
        }

        // --- A pending claim neither reserves the balance nor can be spent early ---

        [Test]
        public async Task PendingClaim_ForSameResource_ReservesNothing()
        {
            var claim = StartPendingCoinsClaim();

            // The whole current balance is spendable, even though a coins reward is on its way.
            Assert.That(wallet.CanSpend("coins", 100), Is.True);
            Assert.That(wallet.TrySpend("coins", 100), Is.True);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(0));

            api.AnswerHeld(ApiResult.Success());
            await claim;
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(50));
        }

        [Test]
        public async Task PendingReward_IsNotSpendableBeforeItIsGranted()
        {
            var claim = StartPendingCoinsClaim();

            // 100 owned + 50 pending: only the 100 counts until the server accepts the claim.
            Assert.That(wallet.CanSpend("coins", 120), Is.False);
            Assert.That(wallet.TrySpend("coins", 120), Is.False);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(balanceEvents, Is.Empty);

            api.AnswerHeld(ApiResult.Success());
            await claim;

            // Granted now, so the same spend goes through.
            Assert.That(wallet.TrySpend("coins", 120), Is.True);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(30));
        }

        // --- Other resources are untouched by a pending claim ---

        [Test]
        public async Task TrySpend_OtherResourceWhileCoinsClaimPending_WorksAndLeavesClaimAlone()
        {
            var claim = StartPendingCoinsClaim();

            Assert.That(wallet.TrySpend("lives", 1), Is.True);

            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(2));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(manager.GetClaim(RewardId).AttemptCount, Is.EqualTo(1));

            api.AnswerHeld(ApiResult.Success());
            var record = await claim;

            Assert.That(record.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(2));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
        }
    }
}

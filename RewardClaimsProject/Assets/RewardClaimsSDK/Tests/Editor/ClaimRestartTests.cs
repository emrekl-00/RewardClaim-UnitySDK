using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    // Claims and balances must survive the app being closed or killed at any moment. A "restart" is
    // a new store + wallet + manager on the SAME storage, with a new fake network: exactly what the
    // next launch has, nothing kept in memory.
    public class ClaimRestartTests
    {
        private const string RewardId = "level-10-first-completion";

        private static readonly ResourceDefinition Coins = ResourceDefinition.Create("coins", startingBalance: 100);
        private static readonly ResourceDefinition Gems = ResourceDefinition.Create("gems", startingBalance: 0);

        private InMemoryStorage storage;
        private FakeRewardApi api;
        private ResourceWallet wallet;
        private ClaimManager manager;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemoryStorage();
            Restart();
        }

        [TearDown]
        public void TearDown()
        {
            // Only the current session's requests. A killed session's held request is never answered
            // on purpose: nothing awaits it, so it can't hang the run, and answering it would bring
            // a "dead" manager back to life to write over the storage.
            api.AnswerAllHeld(ApiResult.Failure(ClaimError.Timeout));
        }

        // The next app launch. Without arguments the game's config has only coins.
        private void Restart(params ResourceDefinition[] resources)
        {
            api = new FakeRewardApi();
            var store = new ProgressStore(storage, TestWallets.PlayerId);
            wallet = new ResourceWallet(resources.Length > 0 ? resources : new[] { Coins }, store);
            manager = new ClaimManager(store, wallet, api, TestWallets.PlayerId);
        }

        // Claims a reward whose request never answers, then restarts: the app was killed while
        // the request was on the wire.
        private void KillAppDuringClaim(string rewardId, string resourceId, int amount)
        {
            api.HoldRequests = true;
            _ = manager.ClaimAsync(rewardId, resourceId, amount);
            Restart();
        }

        // The "status" numbers in the raw JSON on disk, oldest claim first: what the next launch reads.
        private int[] SavedStatusNumbers()
        {
            var json = storage.Load(TestWallets.ProgressKey);
            return Regex.Matches(json, "\"status\":(\\d+)")
                .Cast<Match>()
                .Select(match => int.Parse(match.Groups[1].Value))
                .ToArray();
        }

        // --- Killed mid-request: the claim is still there and is finished at the next launch ---

        [Test]
        public async Task Restart_AfterKilledMidRequest_ClaimIsPendingAndRetryGrantsOnce()
        {
            KillAppDuringClaim(RewardId, "coins", 50);

            // The next launch sees the claim the previous session saved before sending.
            var leftOver = manager.GetClaim(RewardId);
            Assert.That(leftOver.Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(leftOver.AttemptCount, Is.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));

            await manager.RetryUnfinishedClaimsAsync();

            var claim = manager.GetClaim(RewardId);
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.AttemptCount, Is.EqualTo(2));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));

            // And it stays granted once across one more launch.
            Restart();
            await manager.RetryUnfinishedClaimsAsync();
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(api.ReceivedPayloads, Is.Empty);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        // --- Each finished status survives a restart and behaves the same afterwards ---

        [Test]
        public async Task Restart_AfterFailure_StaysFailedWithErrorAndRetryGrantsOnce()
        {
            api.Result = ApiResult.Failure(ClaimError.NoConnection);
            await manager.ClaimAsync(RewardId, "coins", 50);

            Restart();

            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(manager.GetClaim(RewardId).LastError, Is.EqualTo(ClaimError.NoConnection));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));

            await manager.RetryUnfinishedClaimsAsync();

            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(manager.GetClaim(RewardId).LastError, Is.EqualTo(ClaimError.None));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task Restart_AfterSuccess_IsNeitherSentNorGrantedAgain()
        {
            await manager.ClaimAsync(RewardId, "coins", 50);

            Restart();
            var again = await manager.ClaimAsync(RewardId, "coins", 50);
            await manager.RetryUnfinishedClaimsAsync();

            Assert.That(again.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(again.AttemptCount, Is.EqualTo(1));
            Assert.That(api.ReceivedPayloads, Is.Empty);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task Restart_AfterRejection_IsNeverSentAgain()
        {
            api.Result = ApiResult.Failure(ClaimError.Rejected, statusCode: 400);
            await manager.ClaimAsync(RewardId, "coins", 50);

            Restart();
            await manager.ClaimAsync(RewardId, "coins", 50);
            await manager.RetryUnfinishedClaimsAsync();

            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Rejected));
            Assert.That(manager.GetClaim(RewardId).LastError, Is.EqualTo(ClaimError.Rejected));
            Assert.That(api.ReceivedPayloads, Is.Empty);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        // --- Balances and claims share one document: saving one never overwrites the other ---

        [Test]
        public async Task Restart_AfterSpendingWhileClaimPending_KeepsBothBalanceAndClaim()
        {
            api.HoldRequests = true;
            _ = manager.ClaimAsync(RewardId, "coins", 50);

            // The player spends while the claim is on the wire, then the app is killed.
            Assert.That(wallet.TrySpend("coins", 30), Is.True);
            Restart();

            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(70));
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Pending));

            // Finishing the claim adds on top of the spent balance.
            await manager.RetryUnfinishedClaimsAsync();
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(70 + 50));
        }

        // --- Every status change reaches disk, checked in the raw JSON ---

        [Test]
        public async Task EveryStatusChange_IsInTheSavedJsonRightAway()
        {
            // The numbers are the save format (see ClaimStatus): Pending 0, Succeeded 1, Failed 2, Rejected 3.
            int[] savedWhenSent = null;
            api.OnSend = _ => savedWhenSent = SavedStatusNumbers();
            api.Result = ApiResult.Failure(ClaimError.Timeout);

            await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(savedWhenSent, Is.EqualTo(new[] { 0 }), "Pending is saved before the request goes out");
            Assert.That(SavedStatusNumbers(), Is.EqualTo(new[] { 2 }), "Failed is saved when the request fails");

            api.Result = ApiResult.Success();
            await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(savedWhenSent, Is.EqualTo(new[] { 0 }), "a retry is saved as Pending before it goes out");
            Assert.That(SavedStatusNumbers(), Is.EqualTo(new[] { 1 }), "Succeeded is saved when the server accepts");

            api.Result = ApiResult.Failure(ClaimError.Rejected, statusCode: 400);
            await manager.ClaimAsync("another-reward", "coins", 10);

            Assert.That(SavedStatusNumbers(), Is.EqualTo(new[] { 1, 3 }), "Rejected is saved when the server refuses");
        }

        // --- A game update removed a resource that an unfinished claim was for ---

        [Test]
        public async Task RetryUnfinishedClaimsAsync_ResourceRemovedFromConfig_SkipsThatClaimAndRetriesTheRest()
        {
            // Arrange: version 1 has gems; a gems claim and a coins claim both fail.
            Restart(Coins, Gems);
            api.Result = ApiResult.Failure(ClaimError.NoConnection);
            await manager.ClaimAsync("gems-reward", "gems", 5);
            await manager.ClaimAsync(RewardId, "coins", 50);

            // Version 2 removed gems from the config.
            Restart(Coins);
            LogAssert.Expect(LogType.Warning, new Regex("gems-reward"));

            // Act: must not fault, even though one claim can no longer be granted.
            await manager.RetryUnfinishedClaimsAsync();

            // Assert: the coins claim was sent and granted, the gems claim was left exactly as saved.
            Assert.That(api.ReceivedPayloads.ConvertAll(p => p.RewardId), Is.EqualTo(new[] { RewardId }));
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));

            var stale = manager.GetClaim("gems-reward");
            Assert.That(stale.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(stale.LastError, Is.EqualTo(ClaimError.NoConnection));
            Assert.That(stale.AttemptCount, Is.EqualTo(1));

            // Still in the save, so it can be finished if a later update brings gems back.
            Restart(Coins, Gems);
            await manager.RetryUnfinishedClaimsAsync();
            Assert.That(manager.GetClaim("gems-reward").Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("gems"), Is.EqualTo(5));
        }
    }
}

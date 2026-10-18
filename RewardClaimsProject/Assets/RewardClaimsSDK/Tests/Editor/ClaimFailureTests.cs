using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    // What happens when the API fails: which errors are retried, which are final, and that nothing
    // thrown by the API reaches the game. A "restart" is a new manager on the SAME storage.
    public class ClaimFailureTests
    {
        private const string RewardId = "level-10-first-completion";

        private static readonly ResourceDefinition Coins = ResourceDefinition.Create("coins", startingBalance: 100);

        private InMemoryStorage storage;
        private FakeRewardApi api;
        private ResourceWallet wallet;
        private ClaimManager manager;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemoryStorage();
            api = new FakeRewardApi();
            Launch();
        }

        [TearDown]
        public void TearDown()
        {
            api.AnswerAllHeld(ApiResult.Failure(ClaimError.Timeout));
        }

        private void Launch()
        {
            var store = new ProgressStore(storage, TestWallets.PlayerId);
            wallet = new ResourceWallet(new[] { Coins }, store);
            manager = new ClaimManager(store, wallet, api, TestWallets.PlayerId);
        }

        // Writes a claim straight to storage as an earlier app session would have left it. Call
        // Launch() afterwards so the manager under test loads it.
        private void SaveClaimFromPreviousSession(string rewardId, int amount, Action<ClaimRecord> setStatus)
        {
            var previousSession = new ProgressStore(storage, TestWallets.PlayerId);
            var claim = new ClaimRecord(rewardId, "coins", amount);
            claim.MarkAttempt();
            setStatus(claim);
            previousSession.Data.claims.Add(claim);
            previousSession.Save();
        }

        // --- Retryable errors: the claim fails, grants nothing, and can be sent again ---

        [TestCase(ClaimError.NoConnection)]
        [TestCase(ClaimError.Timeout)]
        [TestCase(ClaimError.ServerError)]
        [TestCase(ClaimError.InvalidResponse)]
        public async Task ClaimAsync_RetryableError_FailsWithThatErrorAndGrantsNothing(ClaimError error)
        {
            api.Result = ApiResult.Failure(error);

            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(claim.LastError, Is.EqualTo(error));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));

            // Survives a restart, still failed and still not granted.
            Launch();
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(manager.GetClaim(RewardId).LastError, Is.EqualTo(error));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public async Task RetryAfterFailure_Succeeds_GrantsOnceAndClearsError()
        {
            api.Result = ApiResult.Failure(ClaimError.NoConnection);
            await manager.ClaimAsync(RewardId, "coins", 50);

            // The connection comes back.
            api.Result = ApiResult.Success();
            await manager.RetryUnfinishedClaimsAsync();

            var claim = manager.GetClaim(RewardId);
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.AttemptCount, Is.EqualTo(2));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.None));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));

            // Retrying again sends nothing more.
            await manager.RetryUnfinishedClaimsAsync();
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(2));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        // --- Rejected (4xx): final, never sent again ---

        [Test]
        public async Task ClaimAsync_Rejected_IsFinalAndNeverSentAgain()
        {
            api.Result = ApiResult.Failure(ClaimError.Rejected, statusCode: 400);

            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Rejected));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Rejected));

            // Even if the server would now say yes, a rejected claim isn't asked again.
            api.Result = ApiResult.Success();
            await manager.ClaimAsync(RewardId, "coins", 50);
            await manager.RetryUnfinishedClaimsAsync();

            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Launch();
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Rejected));
        }

        // --- The API throws: the claim fails, the exception is logged, the game never sees it ---

        [Test]
        public async Task ClaimAsync_ApiThrowsSynchronously_FailsWithUnknownAndLogs()
        {
            api.OnSend = _ => throw new InvalidOperationException("API exploded");
            LogAssert.Expect(LogType.Exception, new Regex("API exploded"));
            var statuses = new List<ClaimStatus>();
            manager.ClaimStatusChanged += c => statuses.Add(c.Status);

            // A throw here would fail the test: the task must complete normally.
            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Unknown));
            Assert.That(statuses, Is.EqualTo(new[] { ClaimStatus.Pending, ClaimStatus.Failed }));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Launch();
            Assert.That(manager.GetClaim(RewardId).LastError, Is.EqualTo(ClaimError.Unknown));
        }

        [Test]
        public async Task ClaimAsync_ApiReturnsFaultedTask_FailsWithUnknownAndLogs()
        {
            api.Fault = new InvalidOperationException("API task faulted");
            LogAssert.Expect(LogType.Exception, new Regex("API task faulted"));

            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Unknown));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public async Task ClaimAsync_ApiReturnsNullResult_FailsWithUnknownAndLogs()
        {
            api.Result = null;
            LogAssert.Expect(LogType.Exception, new Regex("returned no result"));

            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Unknown));
        }

        [Test]
        public async Task ClaimAsync_AfterApiThrew_RetriesAndGrantsOnce()
        {
            api.Fault = new InvalidOperationException("API task faulted");
            LogAssert.Expect(LogType.Exception, new Regex("API task faulted"));
            await manager.ClaimAsync(RewardId, "coins", 50);

            api.Fault = null;
            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.AttemptCount, Is.EqualTo(2));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        // --- A game listener throws after the reward was granted: still granted exactly once ---

        [Test]
        public async Task ClaimAsync_BalanceChangedListenerThrows_ClaimStaysSucceededAndIsNotGrantedAgain()
        {
            wallet.BalanceChanged += (_, __) => throw new InvalidOperationException("Game UI bug");

            // The game's own exception reaches the game, where it awaits the claim...
            Assert.ThrowsAsync<InvalidOperationException>(() => manager.ClaimAsync(RewardId, "coins", 50));

            // ...but the reward was saved as granted before the listener ran, and it stays that way.
            var claim = manager.GetClaim(RewardId);
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));

            // Claiming or retrying again must not grant it a second time.
            var again = await manager.ClaimAsync(RewardId, "coins", 50);
            await manager.RetryUnfinishedClaimsAsync();

            Assert.That(again.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
            Launch();
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        // --- RetryUnfinishedClaimsAsync ---

        [Test]
        public async Task RetryUnfinishedClaimsAsync_RetriesFailedAndLeftoverPending_SkipsFinishedOnes()
        {
            // Arrange: one claim in each status, left by the previous app session.
            SaveClaimFromPreviousSession("failed", 10, c => c.MarkFailed(ClaimError.Timeout));
            SaveClaimFromPreviousSession("pending", 20, _ => { });
            SaveClaimFromPreviousSession("succeeded", 40, c => c.MarkSucceeded());
            SaveClaimFromPreviousSession("rejected", 80, c => c.MarkRejected());
            Launch();

            // Act
            await manager.RetryUnfinishedClaimsAsync();

            // Assert: only the two unfinished claims were sent, each once, and granted.
            Assert.That(api.ReceivedPayloads.ConvertAll(p => p.RewardId), Is.EquivalentTo(new[] { "failed", "pending" }));
            Assert.That(manager.GetClaim("failed").Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(manager.GetClaim("pending").Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(manager.GetClaim("succeeded").Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(manager.GetClaim("rejected").Status, Is.EqualTo(ClaimStatus.Rejected));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100 + 10 + 20));
        }

        [Test]
        public async Task RetryUnfinishedClaimsAsync_ClaimInFlight_IsNotSentAgain()
        {
            SaveClaimFromPreviousSession("failed", 10, c => c.MarkFailed(ClaimError.Timeout));
            Launch();
            api.HoldRequests = true;
            var inFlight = manager.ClaimAsync(RewardId, "coins", 50);

            var retry = manager.RetryUnfinishedClaimsAsync();

            // Only the failed claim was sent by the retry; the in-flight one still has its single request.
            Assert.That(api.ReceivedPayloads.ConvertAll(p => p.RewardId), Is.EqualTo(new[] { RewardId, "failed" }));
            api.AnswerAllHeld(ApiResult.Success());
            await retry;
            await inFlight;

            Assert.That(manager.GetClaim(RewardId).AttemptCount, Is.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100 + 50 + 10));
        }

        [Test]
        public async Task RetryUnfinishedClaimsAsync_ListenerClaimsAnotherReward_DoesNotBreakTheRetry()
        {
            // Arrange: when the old claim succeeds, the game hands out a bonus reward.
            SaveClaimFromPreviousSession("failed", 10, c => c.MarkFailed(ClaimError.Timeout));
            Launch();
            Task<ClaimRecord> bonus = null;
            manager.ClaimStatusChanged += c =>
            {
                if (c.RewardId == "failed" && c.Status == ClaimStatus.Succeeded)
                    bonus = manager.ClaimAsync("bonus", "coins", 5);
            };

            // Act: the bonus claim is added to the list while the retry is running.
            await manager.RetryUnfinishedClaimsAsync();
            await bonus;

            // Assert
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100 + 10 + 5));
            Assert.That(manager.Claims, Has.Count.EqualTo(2));
        }

        [Test]
        public void RetryUnfinishedClaimsAsync_NothingToRetry_CompletesAtOnce()
        {
            var retry = manager.RetryUnfinishedClaimsAsync();

            Assert.That(retry.IsCompleted, Is.True);
            Assert.That(api.ReceivedPayloads, Is.Empty);
        }
    }
}

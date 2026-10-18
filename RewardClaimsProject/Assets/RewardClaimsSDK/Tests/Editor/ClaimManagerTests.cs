using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    // A "restart" is a new store + wallet + manager on the SAME storage, like the next app launch.
    public class ClaimManagerTests
    {
        private const string RewardId = "level-10-first-completion";

        private static readonly ResourceDefinition Coins = ResourceDefinition.Create("coins", startingBalance: 100);
        private static readonly ResourceDefinition Lives = ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5);

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
            // Every test answers its own held requests; this only matters when a test failed half-way.
            api.AnswerAllHeld(ApiResult.Failure(ClaimError.Timeout));
        }

        private void Launch()
        {
            var store = new ProgressStore(storage, TestWallets.PlayerId);
            wallet = new ResourceWallet(new[] { Coins, Lives }, store);
            manager = new ClaimManager(store, wallet, api, TestWallets.PlayerId);
        }

        // What the next launch would read from storage, without replacing the objects under test.
        private ClaimRecord SavedClaim(string rewardId)
        {
            return new ProgressStore(storage, TestWallets.PlayerId).Data.claims.Find(c => c.RewardId == rewardId);
        }

        // Writes a claim straight to storage as an earlier app session would have left it, then
        // launches again so the manager under test loads it.
        private void LaunchWithClaimFromPreviousSession(Action<ClaimRecord> setStatus)
        {
            var previousSession = new ProgressStore(storage, TestWallets.PlayerId);
            var claim = new ClaimRecord(RewardId, "coins", 50);
            claim.MarkAttempt();
            setStatus(claim);
            previousSession.Data.claims.Add(claim);
            previousSession.Save();

            Launch();
        }

        [Test]
        public async Task ClaimAsync_Success_AddsRewardAndSucceeds()
        {
            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.None));
            Assert.That(claim.AttemptCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ClaimAsync_SendsPayloadWithPlayerRewardResourceAndAmount()
        {
            await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            var payload = api.ReceivedPayloads[0];
            Assert.That(payload.PlayerId, Is.EqualTo(TestWallets.PlayerId));
            Assert.That(payload.RewardId, Is.EqualTo(RewardId));
            Assert.That(payload.ResourceId, Is.EqualTo("coins"));
            Assert.That(payload.Amount, Is.EqualTo(50));
        }

        [Test]
        public async Task ClaimAsync_Success_RaisesPendingThenSucceeded()
        {
            // The record is the same object each time, so copy its status when the event fires.
            var statuses = new List<ClaimStatus>();
            manager.ClaimStatusChanged += claim => statuses.Add(claim.Status);

            await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(statuses, Is.EqualTo(new[] { ClaimStatus.Pending, ClaimStatus.Succeeded }));
        }

        [Test]
        public async Task ClaimAsync_SavesPendingClaimBeforeSending()
        {
            ClaimRecord savedWhenSent = null;
            api.OnSend = _ => savedWhenSent = SavedClaim(RewardId);

            await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(savedWhenSent, Is.Not.Null);
            Assert.That(savedWhenSent.Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(savedWhenSent.AttemptCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Restart_AfterSuccess_KeepsSucceededClaimAndBalance()
        {
            await manager.ClaimAsync(RewardId, "coins", 50);

            Launch();

            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task Restart_AfterSuccessWithResourceAtMax_KeepsSucceededClaim()
        {
            // Arrange: lives at max, so the reward is fully clamped and the wallet won't save.
            wallet.Add("lives", 2);

            // Act
            var claim = await manager.ClaimAsync(RewardId, "lives", 1);
            Launch();

            // Assert: the status still reached disk.
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(manager.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(5));
        }

        [Test]
        public async Task ClaimAsync_Failure_MarksFailedAndGrantsNothing()
        {
            api.Result = ApiResult.Failure(ClaimError.Timeout);
            var statuses = new List<ClaimStatus>();
            manager.ClaimStatusChanged += claim => statuses.Add(claim.Status);

            var result = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(result.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(result.LastError, Is.EqualTo(ClaimError.Timeout));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(statuses, Is.EqualTo(new[] { ClaimStatus.Pending, ClaimStatus.Failed }));
            Assert.That(SavedClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Failed));
        }

        // --- Idempotency: one reward id is granted at most once, however often it is claimed ---

        [Test]
        public async Task ClaimAsync_SucceededRewardIdAgain_ReturnsSameRecordWithoutSending()
        {
            var first = await manager.ClaimAsync(RewardId, "coins", 50);

            var second = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(second, Is.SameAs(first));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_TwoCallsWhileRequestInFlight_ShareOneRequestAndGrantOnce()
        {
            api.HoldRequests = true;

            // Act: a double tap; the second call comes before the server has answered the first.
            var first = manager.ClaimAsync(RewardId, "coins", 50);
            var second = manager.ClaimAsync(RewardId, "coins", 50);

            // Assert while waiting: one request, the same task, nothing granted yet.
            Assert.That(second, Is.SameAs(first));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));

            api.AnswerHeld(ApiResult.Success());
            var claim = await first;

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.AttemptCount, Is.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_AfterInFlightClaimSucceeded_ReturnsRecordWithoutSending()
        {
            api.HoldRequests = true;
            var inFlight = manager.ClaimAsync(RewardId, "coins", 50);
            api.AnswerHeld(ApiResult.Success());
            var claim = await inFlight;

            var third = manager.ClaimAsync(RewardId, "coins", 50);

            // Already finished: the record's status answered it, not a leftover in-flight entry.
            Assert.That(third, Is.Not.SameAs(inFlight));
            Assert.That(third.IsCompleted, Is.True);
            Assert.That(await third, Is.SameAs(claim));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_AfterInFlightClaimFailed_SendsAgain()
        {
            api.HoldRequests = true;
            var inFlight = manager.ClaimAsync(RewardId, "coins", 50);
            api.AnswerHeld(ApiResult.Failure(ClaimError.Timeout));
            await inFlight;

            // A stale in-flight entry would hand back the old, failed task here instead of retrying.
            var retry = manager.ClaimAsync(RewardId, "coins", 50);
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(2));
            api.AnswerHeld(ApiResult.Success());
            var claim = await retry;

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_FailedRewardIdAgain_RetriesAndGrantsOnce()
        {
            api.Result = ApiResult.Failure(ClaimError.Timeout);
            var first = await manager.ClaimAsync(RewardId, "coins", 50);

            api.Result = ApiResult.Success();
            var second = await manager.ClaimAsync(RewardId, "coins", 50);

            // Same record, sent a second time; the earlier failure granted nothing.
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(second.AttemptCount, Is.EqualTo(2));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(2));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));

            // And once it succeeded, a third call changes nothing.
            await manager.ClaimAsync(RewardId, "coins", 50);
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(2));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_PendingFromPreviousSession_RetriesAndGrantsOnce()
        {
            // Arrange: the app was killed while the first request was in flight.
            LaunchWithClaimFromPreviousSession(_ => { });

            // Act
            var retried = await manager.ClaimAsync(RewardId, "coins", 50);

            // Assert
            Assert.That(retried.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(retried.AttemptCount, Is.EqualTo(2));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
            Assert.That(SavedClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
        }

        [Test]
        public async Task ClaimAsync_RejectedRewardIdAgain_ReturnsRecordWithoutSending()
        {
            LaunchWithClaimFromPreviousSession(leftOver => leftOver.MarkRejected());

            var claim = await manager.ClaimAsync(RewardId, "coins", 50);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Rejected));
            Assert.That(api.ReceivedPayloads, Is.Empty);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public async Task ClaimAsync_ExistingRewardIdWithDifferentAmount_KeepsOriginalAndWarns()
        {
            var first = await manager.ClaimAsync(RewardId, "coins", 50);
            LogAssert.Expect(LogType.Warning, new Regex("already claimed as 50 coins"));

            var second = await manager.ClaimAsync(RewardId, "coins", 80);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Amount, Is.EqualTo(50));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
        }

        [Test]
        public async Task ClaimAsync_RetryWithDifferentResource_SendsTheOriginalClaim()
        {
            api.Result = ApiResult.Failure(ClaimError.Timeout);
            await manager.ClaimAsync(RewardId, "coins", 50);
            api.Result = ApiResult.Success();
            LogAssert.Expect(LogType.Warning, new Regex("already claimed as 50 coins"));

            await manager.ClaimAsync(RewardId, "lives", 1);

            var retryPayload = api.ReceivedPayloads[1];
            Assert.That(retryPayload.ResourceId, Is.EqualTo("coins"));
            Assert.That(retryPayload.Amount, Is.EqualTo(50));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(150));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(3));
        }

        [Test]
        public async Task Claims_ListsEveryClaim()
        {
            await manager.ClaimAsync("reward-a", "coins", 10);
            await manager.ClaimAsync("reward-b", "lives", 1);

            Assert.That(manager.Claims, Has.Count.EqualTo(2));
            Assert.That(manager.Claims[0].RewardId, Is.EqualTo("reward-a"));
            Assert.That(manager.Claims[1].RewardId, Is.EqualTo("reward-b"));
        }

        [Test]
        public void GetClaim_UnknownRewardId_ReturnsNull()
        {
            Assert.That(manager.GetClaim("never-claimed"), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void ClaimAsync_EmptyRewardId_Throws(string rewardId)
        {
            Assert.ThrowsAsync<ArgumentException>(() => manager.ClaimAsync(rewardId, "coins", 50));
        }

        [Test]
        public void ClaimAsync_UnknownResource_Throws()
        {
            Assert.ThrowsAsync<ArgumentException>(() => manager.ClaimAsync(RewardId, "gems", 50));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void ClaimAsync_AmountNotPositive_Throws(int amount)
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.ClaimAsync(RewardId, "coins", amount));
        }

        [Test]
        public void ClaimAsync_InvalidInput_SendsAndSavesNothing()
        {
            Assert.ThrowsAsync<ArgumentException>(() => manager.ClaimAsync(RewardId, "gems", 50));

            Assert.That(api.ReceivedPayloads, Is.Empty);
            Assert.That(manager.Claims, Is.Empty);
        }

        [Test]
        public void ApiResultFailure_WithoutError_Throws()
        {
            Assert.Throws<ArgumentException>(() => ApiResult.Failure(ClaimError.None));
        }
    }
}

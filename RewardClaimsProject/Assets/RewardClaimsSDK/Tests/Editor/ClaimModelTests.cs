using NUnit.Framework;
using UnityEngine;

namespace RewardClaims.Tests
{
    public class ClaimModelTests
    {
        private const string Key = "RewardClaims.Progress.test-player";

        [Test]
        public void Payload_ToJson_MatchesApiShape()
        {
            var claim = new ClaimRecord("level-10-first-completion", "coins", 100);
            var payload = ClaimPayload.From("test-player", claim);

            var json = JsonUtility.ToJson(payload);

            // The exact body the case's API expects; field names become the snake_case keys.
            Assert.That(json, Is.EqualTo(
                "{\"player_id\":\"test-player\",\"reward_id\":\"level-10-first-completion\",\"resource\":\"coins\",\"amount\":100}"));
        }

        [Test]
        public void NewRecord_StartsPendingWithNoErrorAndNoAttempts()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);

            Assert.That(claim.RewardId, Is.EqualTo("reward-1"));
            Assert.That(claim.ResourceId, Is.EqualTo("coins"));
            Assert.That(claim.Amount, Is.EqualTo(100));
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.None));
            Assert.That(claim.AttemptCount, Is.EqualTo(0));
        }

        [Test]
        public void MarkAttempt_SetsPendingAndCountsAttempts()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);

            claim.MarkAttempt();
            claim.MarkAttempt();

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(claim.AttemptCount, Is.EqualTo(2));
        }

        [Test]
        public void MarkAttempt_AfterFailure_KeepsLastErrorForTheGameToShow()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);
            claim.MarkAttempt();
            claim.MarkFailed(ClaimError.Timeout);

            claim.MarkAttempt();

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Pending));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Timeout));
            Assert.That(claim.AttemptCount, Is.EqualTo(2));
        }

        [Test]
        public void MarkFailed_SetsFailedWithError()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);

            claim.MarkFailed(ClaimError.NoConnection);

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.NoConnection));
        }

        [Test]
        public void MarkSucceeded_SetsSucceededAndClearsError()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);
            claim.MarkFailed(ClaimError.ServerError);

            claim.MarkSucceeded();

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.None));
        }

        [Test]
        public void MarkRejected_SetsRejectedWithRejectedError()
        {
            var claim = new ClaimRecord("reward-1", "coins", 100);

            claim.MarkRejected();

            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Rejected));
            Assert.That(claim.LastError, Is.EqualTo(ClaimError.Rejected));
        }

        [Test]
        public void Progress_WithBalancesAndClaim_RoundTripsThroughStore()
        {
            var storage = new InMemoryStorage();
            var store = new ProgressStore(storage, "test-player");
            store.Data.balances.Add(new ResourceBalance { resourceId = "coins", balance = 70 });
            var claim = new ClaimRecord("level-10-first-completion", "coins", 100);
            claim.MarkAttempt();
            claim.MarkFailed(ClaimError.Timeout);
            store.Data.claims.Add(claim);
            store.Save();

            var reloaded = new ProgressStore(storage, "test-player");

            Assert.That(reloaded.Data.balances.Count, Is.EqualTo(1));
            Assert.That(reloaded.Data.balances[0].resourceId, Is.EqualTo("coins"));
            Assert.That(reloaded.Data.balances[0].balance, Is.EqualTo(70));
            Assert.That(reloaded.Data.claims.Count, Is.EqualTo(1));
            var loaded = reloaded.Data.claims[0];
            Assert.That(loaded.RewardId, Is.EqualTo("level-10-first-completion"));
            Assert.That(loaded.ResourceId, Is.EqualTo("coins"));
            Assert.That(loaded.Amount, Is.EqualTo(100));
            Assert.That(loaded.Status, Is.EqualTo(ClaimStatus.Failed));
            Assert.That(loaded.LastError, Is.EqualTo(ClaimError.Timeout));
            Assert.That(loaded.AttemptCount, Is.EqualTo(1));
        }

        [Test]
        public void Save_WithClaim_WritesEnumsAsNumbers()
        {
            var storage = new InMemoryStorage();
            var store = new ProgressStore(storage, "test-player");
            store.Data.balances.Add(new ResourceBalance { resourceId = "coins", balance = 70 });
            var claim = new ClaimRecord("level-10-first-completion", "coins", 100);
            claim.MarkAttempt();
            claim.MarkFailed(ClaimError.Timeout);
            store.Data.claims.Add(claim);

            store.Save();

            // Failed = 2, Timeout = 2: why the enum values must never be renumbered.
            Assert.That(storage.Load(Key), Is.EqualTo(
                "{\"balances\":[{\"resourceId\":\"coins\",\"balance\":70}]," +
                "\"claims\":[{\"rewardId\":\"level-10-first-completion\",\"resourceId\":\"coins\",\"amount\":100," +
                "\"status\":2,\"lastError\":2,\"attemptCount\":1}]}"));
        }

        [Test]
        public void Load_SaveFromBeforeClaimsExisted_StartsWithEmptyClaims()
        {
            var storage = new InMemoryStorage();
            // Exactly what Step 7 wrote: balances only, no "claims" field.
            storage.Save(Key, "{\"balances\":[{\"resourceId\":\"coins\",\"balance\":70}]}");

            var store = new ProgressStore(storage, "test-player");

            Assert.That(store.Data.balances.Count, Is.EqualTo(1));
            Assert.That(store.Data.balances[0].balance, Is.EqualTo(70));
            Assert.That(store.Data.claims, Is.Not.Null.And.Empty);
        }
    }
}

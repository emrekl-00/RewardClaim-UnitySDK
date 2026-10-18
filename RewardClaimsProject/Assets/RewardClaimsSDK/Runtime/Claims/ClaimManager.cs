using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>Sends reward claims to the reward API and grants the reward once the server accepts it.</summary>
    public class ClaimManager
    {
        private readonly ProgressStore store;
        private readonly ResourceWallet wallet;
        private readonly IRewardApi api;
        private readonly string playerId;

        // Claims whose request hasn't answered yet, by reward id. Main thread only, like the rest of
        // the SDK, so a plain Dictionary is enough.
        private readonly Dictionary<string, Task<ClaimRecord>> inFlight = new Dictionary<string, Task<ClaimRecord>>();

        /// <summary>Raised after a claim's status changes and the change is saved.</summary>
        public event Action<ClaimRecord> ClaimStatusChanged;

        /// <summary>Every claim of this player, oldest first.</summary>
        // A read-only wrapper, so the game can't add or remove claims by casting back to List.
        public IReadOnlyList<ClaimRecord> Claims => store.Data.claims.AsReadOnly();

        // The wallet must be built on the same store: then a claim's status and the reward it grants
        // live in one document and are saved by one write. Internal because games get the manager
        // from the SDK facade, which owns the store.
        internal ClaimManager(ProgressStore store, ResourceWallet wallet, IRewardApi api, string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player id must not be null or empty.", nameof(playerId));

            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.playerId = playerId;
        }

        /// <summary>Claims a reward: sends it to the server and, once accepted, adds it to the wallet.</summary>
        /// <returns>The claim's record; check its <see cref="ClaimRecord.Status"/> for the outcome.</returns>
        /// <remarks>
        /// Safe to call again with the same reward id: a reward is granted at most once. A succeeded or
        /// rejected claim is returned as it is, a claim still in flight returns the same task, and a
        /// failed or unfinished claim is sent again. Network, server and API problems never throw; they
        /// end in <see cref="ClaimStatus.Failed"/> or <see cref="ClaimStatus.Rejected"/>. Invalid
        /// arguments are bugs in the calling code; they surface as a faulted task, so the exception is
        /// thrown where the task is awaited. The same goes for an exception thrown by the game's own
        /// <see cref="ResourceWallet.BalanceChanged"/> listener while the reward is granted: the claim
        /// is then already saved as succeeded and won't be granted again.
        /// </remarks>
        /// <exception cref="ArgumentException">The reward id is empty or the resource is unknown.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The amount is zero or less.</exception>
        // Not an async method, so that a second call can hand back the very same Task as the first.
        public Task<ClaimRecord> ClaimAsync(string rewardId, string resourceId, int amount)
        {
            var argumentError = ValidateClaim(rewardId, resourceId, amount);
            if (argumentError != null)
                return Task.FromException<ClaimRecord>(argumentError);

            var claim = GetClaim(rewardId);
            if (claim == null)
            {
                claim = new ClaimRecord(rewardId, resourceId, amount);
                store.Data.claims.Add(claim);
            }
            else
            {
                // The first claim is the source of truth: it is already saved and may already be on
                // the server with its own resource and amount. Taking the new values would let one
                // reward id grant two different rewards.
                if (claim.ResourceId != resourceId || claim.Amount != amount)
                {
                    Debug.LogWarning($"Reward '{rewardId}' was already claimed as {claim.Amount} {claim.ResourceId}; " +
                                     $"ignoring the new {amount} {resourceId} and keeping the original.");
                }

                // A double tap, or a second screen claiming the same reward: share the request that
                // is already on the wire instead of sending another one.
                if (inFlight.TryGetValue(rewardId, out var running))
                    return running;

                if (claim.Status == ClaimStatus.Succeeded || claim.Status == ClaimStatus.Rejected)
                    return Task.FromResult(claim);

                // Left: Failed, or Pending from a previous app session that was closed mid-request
                // (nothing is in flight for it now). Both are sent again below.
            }

            var attempt = SendAttemptAsync(claim);

            // An API that answers synchronously finishes the attempt before we get here; its finally
            // has already run, so registering it now would leave a stale entry behind.
            if (!attempt.IsCompleted)
                inFlight[rewardId] = attempt;

            return attempt;
        }

        /// <summary>The claim for this reward id, or null when it was never claimed.</summary>
        public ClaimRecord GetClaim(string rewardId)
        {
            return store.Data.claims.Find(claim => claim.RewardId == rewardId);
        }

        /// <summary>Sends every failed or unfinished claim again; call it at startup or when the connection comes back.</summary>
        /// <returns>A task that completes when every retry has answered.</returns>
        /// <remarks>
        /// Succeeded and rejected claims are never sent again, and a claim already in flight is left to
        /// its own request (the returned task doesn't wait for it). The SDK facade will call this
        /// automatically once it exists.
        /// </remarks>
        public Task RetryUnfinishedClaimsAsync()
        {
            // A snapshot: ClaimAsync raises ClaimStatusChanged, and a listener that claims another
            // reward from there would change the list while we are still walking it.
            var unfinished = store.Data.claims
                .Where(claim => claim.Status == ClaimStatus.Failed || claim.Status == ClaimStatus.Pending)
                .Where(claim => !inFlight.ContainsKey(claim.RewardId))
                .ToList();

            // Through ClaimAsync with the record's own values, so every idempotency rule still applies.
            var retries = unfinished.Select(claim => ClaimAsync(claim.RewardId, claim.ResourceId, claim.Amount));
            return Task.WhenAll(retries);
        }

        // One request for a new claim or a retry. It always uses the record's resource and amount,
        // never the arguments of the call that triggered it.
        private async Task<ClaimRecord> SendAttemptAsync(ClaimRecord claim)
        {
            try
            {
                claim.MarkAttempt();

                // Save before sending: if the app is killed while the request is in flight, the claim
                // is still on disk at the next launch and can be finished instead of silently lost.
                SaveAndNotify(claim);

                var payload = ClaimPayload.From(playerId, claim);
                ApiResult result;

                // Only the call into the API is wrapped, never the success handling below. The reward is
                // saved as granted inside wallet.Add, which then raises the game's BalanceChanged; if a
                // listener threw there and this catch covered it, a granted claim would be marked Failed
                // and the next retry would grant it a second time.
                try
                {
                    // No ConfigureAwait(false): in Unity the code after await must run on the main thread,
                    // because it touches the wallet and raises events the game's UI listens to.
                    result = await api.SendClaimAsync(payload);

                    // IRewardApi promises a result, so null is a bug in the API code, like a throw.
                    if (result == null)
                        throw new InvalidOperationException("The reward API returned no result.");
                }
                catch (Exception exception)
                {
                    // A broken or wrapped API must not crash the game: the claim only fails and can be
                    // retried. Logged, because otherwise nobody would ever see the cause.
                    Debug.LogException(exception);
                    claim.MarkFailed(ClaimError.Unknown);
                    SaveAndNotify(claim);
                    return claim;
                }

                if (result.IsSuccess)
                {
                    claim.MarkSucceeded();

                    // Normally wallet.Add saves the whole document once, writing the new balance and the
                    // Succeeded status together. When the add is fully clamped (e.g. lives already at max)
                    // the wallet has nothing to save, so we save the status ourselves; otherwise the next
                    // launch would still see the claim as Pending.
                    if (wallet.Add(claim.ResourceId, claim.Amount) == 0)
                        store.Save();

                    ClaimStatusChanged?.Invoke(claim);
                }
                else if (result.Error == ClaimError.Rejected)
                {
                    // A 4xx means the server understood the claim and refused it (already claimed, not
                    // allowed). Sending the same request again gets the same answer, so it's final.
                    claim.MarkRejected();
                    SaveAndNotify(claim);
                }
                else
                {
                    // No connection, a timeout, a 5xx or a garbled answer say nothing about the claim
                    // itself; the same request can succeed later, so it stays retryable.
                    claim.MarkFailed(result.Error);
                    SaveAndNotify(claim);
                }

                return claim;
            }
            finally
            {
                // However the attempt ends, the next call for this reward must look at the record again
                // instead of getting this finished task.
                inFlight.Remove(claim.RewardId);
            }
        }

        // Returned instead of thrown, so ClaimAsync can hand it back as a faulted task.
        private Exception ValidateClaim(string rewardId, string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(rewardId))
                return new ArgumentException("Reward id must not be null or empty.", nameof(rewardId));
            if (!wallet.HasResource(resourceId))
                return new ArgumentException($"Unknown resource id '{resourceId}'.", nameof(resourceId));
            if (amount <= 0)
                return new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
            return null;
        }

        // Saved before the event is raised, so a listener that throws can't stop the status from
        // reaching disk.
        private void SaveAndNotify(ClaimRecord claim)
        {
            store.Save();
            ClaimStatusChanged?.Invoke(claim);
        }
    }
}

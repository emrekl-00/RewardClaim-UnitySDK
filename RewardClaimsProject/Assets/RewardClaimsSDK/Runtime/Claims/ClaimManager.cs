using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RewardClaims
{
    /// <summary>Sends reward claims to the reward API and grants the reward once the server accepts it.</summary>
    public class ClaimManager
    {
        private readonly ProgressStore store;
        private readonly ResourceWallet wallet;
        private readonly IRewardApi api;
        private readonly string playerId;

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
        /// A reward id that was claimed before returns its existing record without sending again.
        /// Invalid arguments are bugs in the calling code; because this method is async, they surface
        /// as a faulted task, so the exception is thrown where the task is awaited.
        /// </remarks>
        /// <exception cref="ArgumentException">The reward id is empty or the resource is unknown.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The amount is zero or less.</exception>
        public async Task<ClaimRecord> ClaimAsync(string rewardId, string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(rewardId))
                throw new ArgumentException("Reward id must not be null or empty.", nameof(rewardId));
            if (!wallet.HasResource(resourceId))
                throw new ArgumentException($"Unknown resource id '{resourceId}'.", nameof(resourceId));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");

            var existing = GetClaim(rewardId);
            if (existing != null)
                return existing;

            var claim = new ClaimRecord(rewardId, resourceId, amount);
            claim.MarkAttempt();
            store.Data.claims.Add(claim);

            // Save before sending: if the app is killed while the request is in flight, the claim is
            // still on disk at the next launch and can be finished instead of silently lost.
            SaveAndNotify(claim);

            // No ConfigureAwait(false): in Unity the code after await must run on the main thread,
            // because it touches the wallet and raises events the game's UI listens to.
            var result = await api.SendClaimAsync(ClaimPayload.From(playerId, claim));

            if (result.IsSuccess)
            {
                claim.MarkSucceeded();

                // Normally wallet.Add saves the whole document once, writing the new balance and the
                // Succeeded status together. When the add is fully clamped (e.g. lives already at max)
                // the wallet has nothing to save, so we save the status ourselves; otherwise the next
                // launch would still see the claim as Pending.
                if (wallet.Add(resourceId, amount) == 0)
                    store.Save();

                ClaimStatusChanged?.Invoke(claim);
            }
            else
            {
                claim.MarkFailed(result.Error);
                SaveAndNotify(claim);
            }

            return claim;
        }

        /// <summary>The claim for this reward id, or null when it was never claimed.</summary>
        public ClaimRecord GetClaim(string rewardId)
        {
            return store.Data.claims.Find(claim => claim.RewardId == rewardId);
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

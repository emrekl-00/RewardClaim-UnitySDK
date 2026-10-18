using System;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>One reward claim and its current status. Read-only for the game; the SDK updates it.</summary>
    // Private [SerializeField] fields so JsonUtility can save them, public getters so the game
    // can show the status without being able to change it.
    [Serializable]
    public class ClaimRecord
    {
        [SerializeField] private string rewardId;
        [SerializeField] private string resourceId;
        [SerializeField] private int amount;
        [SerializeField] private ClaimStatus status;
        [SerializeField] private ClaimError lastError;
        [SerializeField] private int attemptCount;

        /// <summary>Unique id of the reward; the same reward is never granted twice.</summary>
        public string RewardId => rewardId;

        /// <summary>Id of the resource the reward grants, e.g. "coins".</summary>
        public string ResourceId => resourceId;

        /// <summary>How much of the resource the reward grants.</summary>
        public int Amount => amount;

        /// <summary>Current status of the claim.</summary>
        public ClaimStatus Status => status;

        /// <summary>Error of the last failed attempt, or <see cref="ClaimError.None"/>.</summary>
        public ClaimError LastError => lastError;

        /// <summary>How many times the request has been sent.</summary>
        public int AttemptCount => attemptCount;

        internal ClaimRecord(string rewardId, string resourceId, int amount)
        {
            this.rewardId = rewardId;
            this.resourceId = resourceId;
            this.amount = amount;
            status = ClaimStatus.Pending;
            lastError = ClaimError.None;
            attemptCount = 0;
        }

        // The rules for which change is allowed when live in ClaimManager; these only record it.

        // A request is about to be sent (first try or a retry). lastError is kept so the game can
        // still show why the previous attempt failed while this one runs.
        internal void MarkAttempt()
        {
            status = ClaimStatus.Pending;
            attemptCount++;
        }

        internal void MarkSucceeded()
        {
            status = ClaimStatus.Succeeded;
            lastError = ClaimError.None;
        }

        internal void MarkFailed(ClaimError error)
        {
            status = ClaimStatus.Failed;
            lastError = error;
        }

        internal void MarkRejected()
        {
            status = ClaimStatus.Rejected;
            lastError = ClaimError.Rejected;
        }
    }
}

namespace RewardClaims
{
    /// <summary>Where a reward claim is in its life cycle.</summary>
    // JsonUtility saves enums as numbers, so these values are part of the save format:
    // never reorder or renumber them, only add new ones at the end.
    public enum ClaimStatus
    {
        /// <summary>Saved on the device; the server hasn't confirmed the request yet.</summary>
        Pending = 0,

        /// <summary>The server accepted the claim and the reward was granted.</summary>
        Succeeded = 1,

        /// <summary>The last attempt hit a temporary error; the claim will be retried.</summary>
        Failed = 2,

        /// <summary>The server refused the claim; final, it is never retried.</summary>
        Rejected = 3,
    }
}

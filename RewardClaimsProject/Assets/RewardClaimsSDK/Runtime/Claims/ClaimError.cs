namespace RewardClaims
{
    /// <summary>Why the last attempt of a claim did not succeed.</summary>
    // Saved as a number by JsonUtility, so never reorder or renumber these values; add new ones
    // only at the end, with the next number.
    public enum ClaimError
    {
        /// <summary>No error: the claim hasn't failed (yet).</summary>
        None = 0,

        /// <summary>The device couldn't reach the server.</summary>
        NoConnection = 1,

        /// <summary>The server didn't answer in time.</summary>
        Timeout = 2,

        /// <summary>The server answered with a 5xx error.</summary>
        ServerError = 3,

        /// <summary>The server refused the request (4xx); the claim won't be retried.</summary>
        Rejected = 4,

        /// <summary>The server answered, but the response couldn't be understood.</summary>
        InvalidResponse = 5,

        /// <summary>The API failed in an unexpected way, e.g. threw an exception.</summary>
        Unknown = 6,
    }
}

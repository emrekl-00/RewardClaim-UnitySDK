using System;
using System.Threading.Tasks;
using RewardClaims;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>Starts the SDK's retry of failed and unfinished claims without anyone waiting for it.</summary>
    public static class ClaimRetries
    {
        /// <summary>Sends every unfinished claim again; the results reach the UI through ClaimStatusChanged.</summary>
        public static void RetryUnfinishedInBackground()
        {
            // Discarded on purpose; LogFailuresAsync observes every exception, so none goes unnoticed.
            _ = LogFailuresAsync(RewardClaimsSdk.Claims.RetryUnfinishedClaimsAsync());
        }

        // An async Task, not async void: an exception escaping async void can't be caught by anyone.
        // (Network problems never get here; they end as Failed claims. This is for bugs.)
        private static async Task LogFailuresAsync(Task retries)
        {
            try
            {
                await retries;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

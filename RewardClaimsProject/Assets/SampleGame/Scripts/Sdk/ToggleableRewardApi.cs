using System;
using System.Threading.Tasks;
using RewardClaims;

namespace CoinCatcher
{
    /// <summary>
    /// Wraps the real reward API and can pretend the device is offline, so the demo can show failed
    /// claims and their retries on demand. A decorator: it implements the same
    /// <see cref="IRewardApi"/> as the API it wraps, so the SDK can't tell the two apart.
    /// </summary>
    public class ToggleableRewardApi : IRewardApi
    {
        private readonly IRewardApi inner;

        /// <summary>While true, every claim fails with <see cref="ClaimError.NoConnection"/> without being sent.</summary>
        public bool SimulateOffline { get; set; }

        public ToggleableRewardApi(IRewardApi inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<ApiResult> SendClaimAsync(ClaimPayload payload)
        {
            // The same answer the real API gives when the device has no connection (status 0: no
            // response arrived), so the SDK takes exactly the path a real outage would.
            if (SimulateOffline)
                return Task.FromResult(ApiResult.Failure(ClaimError.NoConnection, 0, "Simulated offline"));

            return inner.SendClaimAsync(payload);
        }
    }
}

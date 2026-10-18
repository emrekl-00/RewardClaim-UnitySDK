using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RewardClaims.Tests
{
    // Stands in for the network: answers with Result right away and remembers every request.
    internal class FakeRewardApi : IRewardApi
    {
        public ApiResult Result { get; set; } = ApiResult.Success();

        public List<ClaimPayload> ReceivedPayloads { get; } = new List<ClaimPayload>();

        // Runs when a request "arrives", so a test can look at what was saved at that moment.
        public Action<ClaimPayload> OnSend { get; set; }

        public Task<ApiResult> SendClaimAsync(ClaimPayload payload)
        {
            ReceivedPayloads.Add(payload);
            OnSend?.Invoke(payload);

            // Already completed, so the manager's await continues immediately; Step 10 needs a fake
            // that can be held open.
            return Task.FromResult(Result);
        }
    }
}

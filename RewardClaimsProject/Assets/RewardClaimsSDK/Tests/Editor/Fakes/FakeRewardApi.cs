using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RewardClaims.Tests
{
    // Stands in for the network and remembers every request. By default it answers with Result right
    // away; with HoldRequests on, each request waits until the test calls AnswerHeld, like a slow network.
    internal class FakeRewardApi : IRewardApi
    {
        private readonly Queue<TaskCompletionSource<ApiResult>> heldRequests = new Queue<TaskCompletionSource<ApiResult>>();

        public ApiResult Result { get; set; } = ApiResult.Success();

        public bool HoldRequests { get; set; }

        public List<ClaimPayload> ReceivedPayloads { get; } = new List<ClaimPayload>();

        public int HeldCount => heldRequests.Count;

        // Runs when a request "arrives", so a test can look at what was saved at that moment.
        public Action<ClaimPayload> OnSend { get; set; }

        public Task<ApiResult> SendClaimAsync(ClaimPayload payload)
        {
            ReceivedPayloads.Add(payload);
            OnSend?.Invoke(payload);

            // Already completed, so the manager's await continues immediately.
            if (!HoldRequests)
                return Task.FromResult(Result);

            // RunContinuationsAsynchronously: when the test answers, the manager's code after its await
            // runs later (on the next editor frame), not inside AnswerHeld. That is how a real response
            // arrives, and it means the test must await the claim before checking the result.
            var request = new TaskCompletionSource<ApiResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            heldRequests.Enqueue(request);
            return request.Task;
        }

        // Answers the oldest held request: "the network answers now".
        public void AnswerHeld(ApiResult result)
        {
            heldRequests.Dequeue().SetResult(result);
        }

        // Safety net for tear-down: a test that failed half-way must not leave requests open.
        public void AnswerAllHeld(ApiResult result)
        {
            while (heldRequests.Count > 0)
                AnswerHeld(result);
        }
    }
}

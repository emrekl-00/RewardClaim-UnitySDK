using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace RewardClaims.Tests
{
    // Real requests to httpbin.org. They need internet and depend on a third-party service that can be
    // slow or down, so they are [Explicit]: a normal test run never runs them, and a broken network can't
    // turn the build red. Run them by hand before a release (by the "Network" category or by name).
    // The EditMode tests already cover every mapping with fake values; these check that the real
    // UnityWebRequest answers look the way those fakes assume.
    public class HttpBinNetworkTests
    {
        private const string AnythingUrl = "https://httpbin.org/anything";
        private const string RewardId = "level-10-first-completion";
        private const string PlayerId = "network-test-player";
        private const int TimeoutSeconds = 10;

        // A safety net above the API's own timeout: if that timeout ever stops working, the test fails
        // instead of hanging the whole run.
        private const float GuardSeconds = 30f;

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator Anything_AcceptsClaim()
        {
            var api = new HttpBinRewardApi(AnythingUrl, TimeoutSeconds);
            var send = SendAsync(api);

            yield return WaitFor(send);

            var result = send.Result;
            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.Error, Is.EqualTo(ClaimError.None));
            Assert.That(result.StatusCode, Is.EqualTo(200));
        }

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator Status500_IsServerError()
        {
            var send = SendAsync(new HttpBinRewardApi("https://httpbin.org/status/500", TimeoutSeconds));

            yield return WaitFor(send);

            AssertFailure(send.Result, ClaimError.ServerError, 500);
        }

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator Status400_IsRejected()
        {
            var send = SendAsync(new HttpBinRewardApi("https://httpbin.org/status/400", TimeoutSeconds));

            yield return WaitFor(send);

            AssertFailure(send.Result, ClaimError.Rejected, 400);
        }

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator Delay10_WithTimeout2_IsTimeout()
        {
            // httpbin waits 10 seconds before answering; the client must give up after 2.
            var stopwatch = Stopwatch.StartNew();
            var send = SendAsync(new HttpBinRewardApi("https://httpbin.org/delay/10", 2));

            yield return WaitFor(send);

            AssertFailure(send.Result, ClaimError.Timeout, 0);
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(10), "The client waited for the server instead of timing out.");
        }

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator UnknownHost_IsNoConnection()
        {
            // .invalid is reserved (RFC 2606), so this name can never resolve.
            var send = SendAsync(new HttpBinRewardApi("https://does-not-exist.invalid/anything", TimeoutSeconds));

            yield return WaitFor(send);

            AssertFailure(send.Result, ClaimError.NoConnection, 0);
        }

        [UnityTest, Explicit("Needs internet; run by hand."), Category("Network")]
        public IEnumerator ClaimManager_EndToEnd_GrantsRewardOnce()
        {
            var store = new ProgressStore(new InMemoryStorage(), PlayerId);
            var wallet = new ResourceWallet(new[] { ResourceDefinition.Create("coins", 0) }, store);
            var api = new CountingRewardApi(new HttpBinRewardApi(AnythingUrl, TimeoutSeconds));
            var manager = new ClaimManager(store, wallet, api, PlayerId);

            var first = manager.ClaimAsync(RewardId, "coins", 100);
            yield return WaitFor(first);

            var claim = first.Result;
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Succeeded), $"Last error: {claim.LastError}");
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(api.Calls, Is.EqualTo(1));

            // Same reward again: already succeeded, so no request goes out and nothing more is granted.
            var second = manager.ClaimAsync(RewardId, "coins", 100);
            yield return WaitFor(second);

            Assert.That(second.Result.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(api.Calls, Is.EqualTo(1));
        }

        // Sends one claim and logs how long httpbin took, so the results can be recorded.
        private static async Task<ApiResult> SendAsync(IRewardApi api)
        {
            var claim = new ClaimRecord(RewardId, "coins", 100);
            var stopwatch = Stopwatch.StartNew();
            var result = await api.SendClaimAsync(ClaimPayload.From(PlayerId, claim));
            Debug.Log($"[Network] {result.Error} ({result.StatusCode}) after {stopwatch.Elapsed.TotalSeconds:0.00}s. {result.Message}");
            return result;
        }

        // [UnityTest] runs as a coroutine, one step per frame, so the player loop keeps running while we
        // wait. That matters: the request and every await continuation are driven by that loop.
        private static IEnumerator WaitFor(Task task)
        {
            var deadline = Time.realtimeSinceStartup + GuardSeconds;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"No answer within {GuardSeconds} seconds.");
                yield return null;
            }

            // Rethrows the task's own exception (not an AggregateException), so a failure reads clearly.
            task.GetAwaiter().GetResult();
        }

        private static void AssertFailure(ApiResult result, ClaimError expectedError, long expectedStatusCode)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(expectedError), result.Message);
            Assert.That(result.StatusCode, Is.EqualTo(expectedStatusCode), result.Message);
        }

        // Wraps the real API to count how many requests actually went out.
        private class CountingRewardApi : IRewardApi
        {
            private readonly IRewardApi inner;

            public int Calls { get; private set; }

            public CountingRewardApi(IRewardApi inner)
            {
                this.inner = inner;
            }

            public Task<ApiResult> SendClaimAsync(ClaimPayload payload)
            {
                Calls++;
                return inner.SendClaimAsync(payload);
            }
        }
    }
}

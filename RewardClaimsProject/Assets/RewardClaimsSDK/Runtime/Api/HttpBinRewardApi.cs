using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace RewardClaims
{
    /// <summary>Sends claims as JSON POST requests with UnityWebRequest; built for httpbin's /anything echo.</summary>
    public class HttpBinRewardApi : IRewardApi
    {
        private readonly string url;
        private readonly int timeoutSeconds;

        /// <summary>Creates a client that posts claims to <paramref name="url"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute http(s) url.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is zero or less.</exception>
        public HttpBinRewardApi(string url, int timeoutSeconds)
        {
            // A bad url would only show up later as a confusing network error, so fail where it's set.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"Url must be an absolute http or https url (was '{url}').", nameof(url));

            // UnityWebRequest treats a timeout of 0 as "wait forever", which would leave a claim pending.
            if (timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds,
                    "Timeout must be greater than 0 seconds.");

            this.url = url;
            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>Posts the claim and reports how the server answered; never throws for network problems.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
        public async Task<ApiResult> SendClaimAsync(ClaimPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            // Only a hint: "reachable" doesn't prove the server is (captive portal, DNS down), so real
            // errors are still mapped below. But "not reachable" is reliable enough to skip a request
            // that can only fail, and saves waiting for a timeout.
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return ApiResult.Failure(ClaimError.NoConnection, 0, "The device reports no internet connection.");

            // The request owns native memory (the request itself and both handlers) that the garbage
            // collector can't see. Dispose frees it right away, also when the request fails.
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(BuildRequestBody(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeoutSeconds;

                // Unity 6 makes AsyncOperation awaitable; the code after it runs on the main thread again.
                await request.SendWebRequest();

                return MapResponse(request.result, request.responseCode, request.error,
                    request.downloadHandler.text, payload.RewardId);
            }
        }

        // UTF-8 bytes of the claim JSON, e.g. {"player_id":..,"reward_id":..,"resource":..,"amount":..}.
        internal static byte[] BuildRequestBody(ClaimPayload payload)
        {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        // Pure (plain values in, ApiResult out) so every outcome can be unit tested without a network.
        internal static ApiResult MapResponse(UnityWebRequest.Result result, long statusCode, string error,
            string body, string expectedRewardId)
        {
            switch (result)
            {
                case UnityWebRequest.Result.Success:
                    return MapSuccess(statusCode, body, expectedRewardId);

                case UnityWebRequest.Result.ConnectionError:
                    // No answer from the server at all; it never saw the claim, or we can't know it did.
                    return ApiResult.Failure(IsTimeoutError(error) ? ClaimError.Timeout : ClaimError.NoConnection,
                        statusCode, error);

                case UnityWebRequest.Result.ProtocolError:
                    return ApiResult.Failure(ErrorForStatusCode(statusCode), statusCode, error);

                case UnityWebRequest.Result.DataProcessingError:
                    // The server answered, but Unity couldn't process the response data.
                    return ApiResult.Failure(ClaimError.InvalidResponse, statusCode, error);

                default:
                    // InProgress: we only map finished requests, so getting here is a bug.
                    return ApiResult.Failure(ClaimError.Unknown, statusCode, $"Unexpected request result {result}. {error}");
            }
        }

        private static ApiResult MapSuccess(long statusCode, string body, string expectedRewardId)
        {
            if (statusCode < 200 || statusCode > 299)
                return ApiResult.Failure(ClaimError.InvalidResponse, statusCode, $"Unexpected status code {statusCode}.");

            // httpbin accepts anything and echoes our request back, so its "acceptance" is just that echo.
            // A matching reward id proves the answer belongs to this claim. A real reward server would
            // send its own confirmation (e.g. a claim id), and we'd check that instead.
            var echoedRewardId = ReadEchoedRewardId(body);

            if (string.IsNullOrEmpty(echoedRewardId))
                return ApiResult.Failure(ClaimError.InvalidResponse, statusCode, "The response has no echoed reward id.");

            if (!string.Equals(echoedRewardId, expectedRewardId, StringComparison.Ordinal))
                return ApiResult.Failure(ClaimError.InvalidResponse, statusCode,
                    $"The response echoed reward id '{echoedRewardId}', expected '{expectedRewardId}'.");

            return ApiResult.Success(statusCode);
        }

        private static string ReadEchoedRewardId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                return JsonUtility.FromJson<HttpBinEcho>(body)?.json?.reward_id;
            }
            catch (ArgumentException)
            {
                // JsonUtility throws ArgumentException for text that isn't JSON, e.g. an HTML error page.
                return null;
            }
        }

        // Unity reports a timeout as a ConnectionError with status code 0, so only the error text tells
        // it apart from "no network". Checked in Unity 6000.6.5f1: a POST to https://httpbin.org/delay/10
        // with timeout 2 ended with error "Request timeout". Matched loosely in case another platform or
        // Unity version words it differently (curl says "timed out"); both errors are retryable anyway,
        // so a miss only changes what the game shows the player.
        private static bool IsTimeoutError(string error)
        {
            return error != null &&
                   (error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static ClaimError ErrorForStatusCode(long statusCode)
        {
            // 408 Request Timeout: the server gave up waiting for our request, so sending it again is fine.
            if (statusCode == 408)
                return ClaimError.Timeout;

            // 429 Too Many Requests: the server is throttling, not refusing the claim, so retry later.
            if (statusCode == 429)
                return ClaimError.ServerError;

            if (statusCode >= 500)
                return ClaimError.ServerError;

            // Any other 4xx: the server refused this request, and sending the same body again won't help.
            if (statusCode >= 400)
                return ClaimError.Rejected;

            // Unity only reports ProtocolError for codes of 400 and up, so this shouldn't happen.
            return ClaimError.InvalidResponse;
        }

        // Only the part of httpbin's answer we read; JsonUtility ignores the other fields. Field names
        // must match the JSON keys, hence the lowercase and snake_case.
        [Serializable]
        private class HttpBinEcho
        {
            public EchoedClaim json;
        }

        [Serializable]
        private class EchoedClaim
        {
            public string reward_id;
        }
    }
}

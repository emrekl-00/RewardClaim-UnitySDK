using System;
using System.Text;
using NUnit.Framework;
using UnityEngine.Networking;

namespace RewardClaims.Tests
{
    // Pure tests: they feed MapResponse the values a finished UnityWebRequest would have, so no request
    // is ever sent. The real network is tested separately (Step 15).
    public class HttpBinRewardApiTests
    {
        private const string Url = "https://httpbin.org/anything";
        private const string RewardId = "level-10-first-completion";

        // A trimmed copy of what httpbin /anything really answered to our claim (Unity 6000.6.5f1).
        private const string EchoBody =
            "{\"args\":{},\"data\":\"{\\\"player_id\\\":\\\"test-player\\\",\\\"reward_id\\\":\\\"level-10-first-completion\\\",\\\"resource\\\":\\\"coins\\\",\\\"amount\\\":100}\"," +
            "\"files\":{},\"form\":{},\"headers\":{\"Content-Type\":\"application/json\",\"Host\":\"httpbin.org\"}," +
            "\"json\":{\"amount\":100,\"player_id\":\"test-player\",\"resource\":\"coins\",\"reward_id\":\"level-10-first-completion\"}," +
            "\"method\":\"POST\",\"url\":\"https://httpbin.org/anything\"}";

        // --- Constructor ---

        [Test]
        public void Constructor_WithHttpsUrlAndPositiveTimeout_Succeeds()
        {
            Assert.DoesNotThrow(() => new HttpBinRewardApi(Url, 10));
            Assert.DoesNotThrow(() => new HttpBinRewardApi("http://localhost:8080/claims", 1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("httpbin.org/anything")]
        [TestCase("/anything")]
        [TestCase("ftp://httpbin.org/anything")]
        public void Constructor_WithInvalidUrl_Throws(string url)
        {
            Assert.Throws<ArgumentException>(() => new HttpBinRewardApi(url, 10));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WithTimeoutZeroOrLess_Throws(int timeoutSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HttpBinRewardApi(Url, timeoutSeconds));
        }

        [Test]
        public void SendClaimAsync_WithNullPayload_FaultsBeforeSending()
        {
            var api = new HttpBinRewardApi(Url, 10);

            // The check runs before the first await, so the task is already faulted.
            var task = api.SendClaimAsync(null);

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.InnerException, Is.TypeOf<ArgumentNullException>());
        }

        // --- Request body ---

        [Test]
        public void BuildRequestBody_IsTheCaseJsonInUtf8()
        {
            var payload = ClaimPayload.From("test-player", new ClaimRecord(RewardId, "coins", 100));

            var body = HttpBinRewardApi.BuildRequestBody(payload);

            Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo(
                "{\"player_id\":\"test-player\",\"reward_id\":\"level-10-first-completion\",\"resource\":\"coins\",\"amount\":100}"));
        }

        // --- Success: httpbin's echo ---

        [Test]
        public void Success_WithMatchingEcho_IsSuccess()
        {
            var result = Map(UnityWebRequest.Result.Success, 200, null, EchoBody);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(ClaimError.None));
            Assert.That(result.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public void Success_WithOtherRewardIdEchoed_IsInvalidResponse()
        {
            var body = "{\"json\":{\"reward_id\":\"some-other-reward\"}}";

            var result = Map(UnityWebRequest.Result.Success, 200, null, body);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Message, Does.Contain("some-other-reward").And.Contain(RewardId));
        }

        [Test]
        public void Success_WithRewardIdInDifferentCase_IsInvalidResponse()
        {
            var body = "{\"json\":{\"reward_id\":\"LEVEL-10-FIRST-COMPLETION\"}}";

            var result = Map(UnityWebRequest.Result.Success, 200, null, body);

            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
        }

        [TestCase("{\"args\":{},\"method\":\"POST\"}")]           // no "json" field
        [TestCase("{\"json\":null}")]                             // httpbin's answer to a body that isn't JSON
        [TestCase("{\"json\":{\"player_id\":\"test-player\"}}")]   // echo without reward_id
        [TestCase("{\"json\":{\"reward_id\":\"\"}}")]              // empty reward_id
        public void Success_WithoutEchoedRewardId_IsInvalidResponse(string body)
        {
            var result = Map(UnityWebRequest.Result.Success, 200, null, body);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
        }

        [TestCase("<html><body>Bad Gateway</body></html>")]
        [TestCase("not json")]
        [TestCase("{\"json\":")]
        public void Success_WithGarbageBody_IsInvalidResponse(string body)
        {
            var result = Map(UnityWebRequest.Result.Success, 200, null, body);

            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Success_WithEmptyBody_IsInvalidResponse(string body)
        {
            var result = Map(UnityWebRequest.Result.Success, 200, null, body);

            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
        }

        [Test]
        public void Success_WithNon2xxStatus_IsInvalidResponse()
        {
            var result = Map(UnityWebRequest.Result.Success, 304, null, EchoBody);

            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
            Assert.That(result.StatusCode, Is.EqualTo(304));
        }

        // --- Connection errors ---

        [TestCase("Request timeout")]       // Unity 6000.6.5f1's text, seen against httpbin.org/delay/10
        [TestCase("Timeout was reached")]
        [TestCase("Operation timed out")]
        public void ConnectionError_WithTimeoutText_IsTimeout(string error)
        {
            var result = Map(UnityWebRequest.Result.ConnectionError, 0, error, "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.Timeout));
            Assert.That(result.StatusCode, Is.EqualTo(0));
            Assert.That(result.Message, Is.EqualTo(error));
        }

        [TestCase("Cannot resolve destination host")]   // Unity's text for an unknown host
        [TestCase("Cannot connect to destination host")]
        [TestCase(null)]
        public void ConnectionError_WithOtherText_IsNoConnection(string error)
        {
            var result = Map(UnityWebRequest.Result.ConnectionError, 0, error, "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.NoConnection));
            Assert.That(result.Message, Is.EqualTo(error));
        }

        // --- HTTP errors ---

        [TestCase(500)]
        [TestCase(502)]
        [TestCase(503)]
        public void ProtocolError_5xx_IsServerError(long statusCode)
        {
            var error = $"HTTP/1.1 {statusCode} Error";

            var result = Map(UnityWebRequest.Result.ProtocolError, statusCode, error, "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.ServerError));
            Assert.That(result.StatusCode, Is.EqualTo(statusCode));
            Assert.That(result.Message, Is.EqualTo(error));
        }

        [Test]
        public void ProtocolError_408_IsTimeout()
        {
            var result = Map(UnityWebRequest.Result.ProtocolError, 408, "HTTP/1.1 408 Request Timeout", "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.Timeout));
            Assert.That(result.StatusCode, Is.EqualTo(408));
        }

        [Test]
        public void ProtocolError_429_IsServerError()
        {
            var result = Map(UnityWebRequest.Result.ProtocolError, 429, "HTTP/1.1 429 Too Many Requests", "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.ServerError));
            Assert.That(result.StatusCode, Is.EqualTo(429));
        }

        [TestCase(400)]
        [TestCase(401)]
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(422)]
        public void ProtocolError_Other4xx_IsRejected(long statusCode)
        {
            var result = Map(UnityWebRequest.Result.ProtocolError, statusCode, $"HTTP/1.1 {statusCode} Error", "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.Rejected));
            Assert.That(result.StatusCode, Is.EqualTo(statusCode));
        }

        // --- Other results ---

        [Test]
        public void DataProcessingError_IsInvalidResponse()
        {
            var result = Map(UnityWebRequest.Result.DataProcessingError, 200, "Data Processing Error, see Download Handler error", "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.InvalidResponse));
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Message, Does.Contain("Data Processing Error"));
        }

        [Test]
        public void InProgress_IsUnknown()
        {
            var result = Map(UnityWebRequest.Result.InProgress, 0, null, "");

            Assert.That(result.Error, Is.EqualTo(ClaimError.Unknown));
        }

        private static ApiResult Map(UnityWebRequest.Result result, long statusCode, string error, string body)
        {
            return HttpBinRewardApi.MapResponse(result, statusCode, error, body, RewardId);
        }
    }
}

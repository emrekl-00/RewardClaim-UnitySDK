using System;

namespace RewardClaims
{
    /// <summary>The outcome of one request to the reward API.</summary>
    public class ApiResult
    {
        /// <summary>True when the server accepted the claim.</summary>
        public bool IsSuccess { get; }

        /// <summary>Why the request failed, or <see cref="ClaimError.None"/> on success.</summary>
        public ClaimError Error { get; }

        /// <summary>HTTP status code of the response, or 0 when no response arrived.</summary>
        // long because that's what UnityWebRequest.responseCode returns.
        public long StatusCode { get; }

        /// <summary>Optional detail for logs, e.g. the server's error text.</summary>
        public string Message { get; }

        private ApiResult(bool isSuccess, ClaimError error, long statusCode, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            StatusCode = statusCode;
            Message = message;
        }

        /// <summary>The server accepted the claim.</summary>
        public static ApiResult Success(long statusCode = 200)
        {
            return new ApiResult(true, ClaimError.None, statusCode, null);
        }

        /// <summary>The request failed for the given reason.</summary>
        /// <exception cref="ArgumentException"><paramref name="error"/> is <see cref="ClaimError.None"/>.</exception>
        public static ApiResult Failure(ClaimError error, long statusCode = 0, string message = null)
        {
            // The claim's status depends on the reason, so a failure without one is a bug in the API code.
            if (error == ClaimError.None)
                throw new ArgumentException("A failure needs an error other than None.", nameof(error));

            return new ApiResult(false, error, statusCode, message);
        }
    }
}

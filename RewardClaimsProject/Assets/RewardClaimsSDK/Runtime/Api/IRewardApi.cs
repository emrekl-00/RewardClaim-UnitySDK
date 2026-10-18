using System.Threading.Tasks;

namespace RewardClaims
{
    /// <summary>Sends claim requests to the reward server; the SDK's only way to reach the network.</summary>
    /// <remarks>
    /// This is the seam that lets tests run without a network (a fake) and lets a game wrap the
    /// real API, e.g. to simulate being offline, without changing SDK code.
    /// </remarks>
    public interface IRewardApi
    {
        /// <summary>Sends one claim and reports how the server answered.</summary>
        /// <remarks>Report problems through <see cref="ApiResult.Failure"/> rather than by throwing.</remarks>
        Task<ApiResult> SendClaimAsync(ClaimPayload payload);
    }
}

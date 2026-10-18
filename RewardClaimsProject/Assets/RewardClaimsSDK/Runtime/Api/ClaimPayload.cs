using System;

namespace RewardClaims
{
    // The JSON body sent to the reward API.
    // JsonUtility uses field names as JSON keys, so these break C# naming on purpose
    // to match the API's snake_case: {"player_id":..,"reward_id":..,"resource":..,"amount":..}.
    [Serializable]
    internal class ClaimPayload
    {
        public string player_id;
        public string reward_id;
        public string resource;
        public int amount;

        internal static ClaimPayload From(string playerId, ClaimRecord claim)
        {
            return new ClaimPayload
            {
                player_id = playerId,
                reward_id = claim.RewardId,
                resource = claim.ResourceId,
                amount = claim.Amount,
            };
        }
    }
}

using System;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>The request body sent to the reward API. Read-only; only the SDK creates it.</summary>
    // Public because IRewardApi is public (a game can wrap the real API, e.g. to simulate being
    // offline), and an interface can't expose an internal type. Private fields + getters keep it
    // read-only, so a wrapper can look at the claim but can't change what gets sent.
    // JsonUtility uses field names as JSON keys, so the fields break C# naming on purpose to match
    // the API's snake_case: {"player_id":..,"reward_id":..,"resource":..,"amount":..}.
    [Serializable]
    public class ClaimPayload
    {
        [SerializeField] private string player_id;
        [SerializeField] private string reward_id;
        [SerializeField] private string resource;
        [SerializeField] private int amount;

        /// <summary>Id of the player claiming the reward.</summary>
        public string PlayerId => player_id;

        /// <summary>Unique id of the reward being claimed.</summary>
        public string RewardId => reward_id;

        /// <summary>Id of the resource the reward grants.</summary>
        public string ResourceId => resource;

        /// <summary>How much of the resource the reward grants.</summary>
        public int Amount => amount;

        private ClaimPayload()
        {
        }

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

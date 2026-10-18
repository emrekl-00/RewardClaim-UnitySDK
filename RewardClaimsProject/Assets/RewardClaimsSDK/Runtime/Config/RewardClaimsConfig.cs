using System.Collections.Generic;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>
    /// Per-game settings for the SDK, stored as an asset and edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Reward Claims/Config", fileName = "RewardClaimsConfig")]
    public class RewardClaimsConfig : ScriptableObject
    {
        [Tooltip("Identifies the player in claim requests and in saved progress.")]
        [SerializeField] private string playerId = "test-player";

        [Tooltip("Endpoint that receives reward claim requests.")]
        [SerializeField] private string apiUrl = "https://httpbin.org/anything";

        [Tooltip("How long a claim request may take before it counts as failed.")]
        [SerializeField] private int requestTimeoutSeconds = 10;

        [Tooltip("Resources the player owns, e.g. coins and lives.")]
        [SerializeField] private List<ResourceDefinition> resources = new List<ResourceDefinition>();

        /// <summary>Identifies the player in claim requests and in saved progress.</summary>
        public string PlayerId
        {
            get => playerId;
            internal set => playerId = value;
        }

        /// <summary>Endpoint that receives reward claim requests.</summary>
        public string ApiUrl
        {
            get => apiUrl;
            internal set => apiUrl = value;
        }

        /// <summary>How long a claim request may take before it counts as failed.</summary>
        public int RequestTimeoutSeconds
        {
            get => requestTimeoutSeconds;
            internal set => requestTimeoutSeconds = value;
        }

        /// <summary>Resources the player owns, e.g. coins and lives.</summary>
        public IReadOnlyList<ResourceDefinition> Resources
        {
            get => resources;
            // Copy so callers can't keep a handle on the serialized list and change it later.
            internal set => resources = new List<ResourceDefinition>(value);
        }

        /// <summary>
        /// Checks the config for mistakes and returns one readable message per problem.
        /// An empty list means the config is valid.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            // Collect every problem instead of stopping at the first, so a designer can fix them all at once.
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(playerId))
                errors.Add("Player id is empty.");

            if (string.IsNullOrWhiteSpace(apiUrl))
                errors.Add("API url is empty.");

            if (requestTimeoutSeconds <= 0)
                errors.Add($"Request timeout must be greater than 0 seconds (was {requestTimeoutSeconds}).");

            var seenIds = new HashSet<string>();
            var duplicateIds = new HashSet<string>();

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];

                if (string.IsNullOrWhiteSpace(resource.Id))
                {
                    // No id to name it by, so point at its position in the Inspector list.
                    errors.Add($"Resource at index {i} has an empty id.");
                    continue;
                }

                // Add returns false when the id is already there; report each duplicate id only once.
                if (!seenIds.Add(resource.Id) && duplicateIds.Add(resource.Id))
                    errors.Add($"Resource id '{resource.Id}' is used more than once.");

                if (resource.StartingBalance < 0)
                    errors.Add($"Resource '{resource.Id}' has a negative starting balance ({resource.StartingBalance}).");

                if (resource.HasMaxBalance && resource.MaxBalance < resource.StartingBalance)
                    errors.Add($"Resource '{resource.Id}' has a max balance ({resource.MaxBalance}) " +
                               $"lower than its starting balance ({resource.StartingBalance}).");
            }

            return errors;
        }
    }
}

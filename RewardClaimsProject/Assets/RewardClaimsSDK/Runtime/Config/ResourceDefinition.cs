using System;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>Describes one player resource (e.g. coins, lives) and its balance rules.</summary>
    [Serializable]
    public class ResourceDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private int startingBalance;

        // Unity can't serialize int?, so "no max" is a separate flag instead of a null maxBalance.
        [SerializeField] private bool hasMaxBalance;
        [SerializeField] private int maxBalance;

        /// <summary>Unique key used to read and change this resource, e.g. "coins".</summary>
        public string Id => id;

        /// <summary>Balance a new player starts with.</summary>
        public int StartingBalance => startingBalance;

        /// <summary>True when the balance is capped at <see cref="MaxBalance"/>.</summary>
        public bool HasMaxBalance => hasMaxBalance;

        /// <summary>Upper limit of the balance; only used when <see cref="HasMaxBalance"/> is true.</summary>
        public int MaxBalance => maxBalance;

        // Designers fill these in the Inspector; code (tests) builds them through this factory.
        internal static ResourceDefinition Create(string id, int startingBalance, int? maxBalance = null)
        {
            return new ResourceDefinition
            {
                id = id,
                startingBalance = startingBalance,
                hasMaxBalance = maxBalance.HasValue,
                maxBalance = maxBalance ?? 0,
            };
        }
    }
}

using System;
using System.Collections.Generic;

namespace RewardClaims
{
    /// <summary>Holds the player's resource balances and applies the spend and max-balance rules.</summary>
    /// <remarks>
    /// Error policy: an unknown resource id or an amount of zero or less is a bug in the calling
    /// code, so it throws. Not having enough to spend is a normal game situation, so
    /// <see cref="TrySpend"/> returns false instead.
    /// </remarks>
    public class ResourceWallet
    {
        private readonly Dictionary<string, ResourceDefinition> definitions = new Dictionary<string, ResourceDefinition>();
        private readonly Dictionary<string, int> balances = new Dictionary<string, int>();

        /// <summary>Creates a wallet where every resource starts at its starting balance.</summary>
        /// <exception cref="ArgumentException">Two definitions share the same id.</exception>
        public ResourceWallet(IEnumerable<ResourceDefinition> resources)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));

            foreach (var resource in resources)
            {
                // Config.Validate() already reports duplicates to designers; this guards code that skips it.
                if (definitions.ContainsKey(resource.Id))
                    throw new ArgumentException($"Resource id '{resource.Id}' is defined more than once.", nameof(resources));

                definitions.Add(resource.Id, resource);
                balances.Add(resource.Id, resource.StartingBalance);
            }
        }

        /// <summary>True when the wallet knows a resource with this id.</summary>
        public bool HasResource(string resourceId)
        {
            return resourceId != null && balances.ContainsKey(resourceId);
        }

        /// <summary>Current balance of the resource.</summary>
        public int GetBalance(string resourceId)
        {
            EnsureKnown(resourceId);
            return balances[resourceId];
        }

        /// <summary>True when the balance is at least <paramref name="amount"/>.</summary>
        public bool CanSpend(string resourceId, int amount)
        {
            EnsureKnown(resourceId);
            EnsurePositive(amount);
            return balances[resourceId] >= amount;
        }

        /// <summary>Spends <paramref name="amount"/> if the balance allows it; otherwise changes nothing.</summary>
        /// <returns>True when the amount was spent, false when the balance was too low.</returns>
        public bool TrySpend(string resourceId, int amount)
        {
            if (!CanSpend(resourceId, amount))
                return false;

            balances[resourceId] -= amount;
            return true;
        }

        /// <summary>Adds up to <paramref name="amount"/>, stopping at the resource's max balance.</summary>
        /// <returns>The amount actually added (0 when the balance is already at max).</returns>
        public int Add(string resourceId, int amount)
        {
            EnsureKnown(resourceId);
            EnsurePositive(amount);

            // Without a max we still cap at int.MaxValue, so a huge reward can't overflow into a negative balance.
            var definition = definitions[resourceId];
            var limit = definition.HasMaxBalance ? definition.MaxBalance : int.MaxValue;
            var room = Math.Max(0, limit - balances[resourceId]);
            var added = Math.Min(amount, room);

            balances[resourceId] += added;
            return added;
        }

        private void EnsureKnown(string resourceId)
        {
            if (!HasResource(resourceId))
                throw new ArgumentException($"Unknown resource id '{resourceId}'.", nameof(resourceId));
        }

        private static void EnsurePositive(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }
    }
}

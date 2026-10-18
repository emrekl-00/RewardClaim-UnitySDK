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
        private readonly ProgressStore store;

        /// <summary>Raised after a balance changes, with the resource id and the new balance.</summary>
        public event Action<string, int> BalanceChanged;

        /// <summary>Creates a wallet with the balances saved in <paramref name="store"/>.</summary>
        /// <remarks>
        /// A resource missing from the save (e.g. added in a game update) gets its starting balance,
        /// and a saved balance above a (newly lowered) max is clamped to the max. Saved entries for
        /// resources no longer in config stay in the save untouched. Internal because games get the
        /// wallet from the SDK facade, which owns the store.
        /// </remarks>
        /// <exception cref="ArgumentException">Two definitions share the same id.</exception>
        internal ResourceWallet(IEnumerable<ResourceDefinition> resources, ProgressStore store)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));
            this.store = store ?? throw new ArgumentNullException(nameof(store));

            var saveNeeded = false;
            foreach (var resource in resources)
            {
                // Config.Validate() already reports duplicates to designers; this guards code that skips it.
                if (definitions.ContainsKey(resource.Id))
                    throw new ArgumentException($"Resource id '{resource.Id}' is defined more than once.", nameof(resources));

                definitions.Add(resource.Id, resource);

                var saved = FindSaved(resource.Id);
                var balance = saved != null ? Math.Min(saved.balance, LimitOf(resource)) : resource.StartingBalance;
                balances.Add(resource.Id, balance);

                if (saved == null || saved.balance != balance)
                {
                    WriteBalance(resource.Id, balance);
                    saveNeeded = true;
                }
            }

            // One write for all fixes, and none at all when the save already matched the config.
            if (saveNeeded)
                store.Save();
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
            Persist(resourceId);
            BalanceChanged?.Invoke(resourceId, balances[resourceId]);
            return true;
        }

        /// <summary>Adds up to <paramref name="amount"/>, stopping at the resource's max balance.</summary>
        /// <returns>The amount actually added (0 when the balance is already at max).</returns>
        public int Add(string resourceId, int amount)
        {
            EnsureKnown(resourceId);
            EnsurePositive(amount);

            var room = Math.Max(0, LimitOf(definitions[resourceId]) - balances[resourceId]);
            var added = Math.Min(amount, room);

            // A fully clamped add changed nothing, so listeners have nothing to redraw.
            if (added == 0)
                return 0;

            balances[resourceId] += added;
            Persist(resourceId);
            BalanceChanged?.Invoke(resourceId, balances[resourceId]);
            return added;
        }

        // Without a max we still cap at int.MaxValue, so a huge reward can't overflow into a negative balance.
        private static int LimitOf(ResourceDefinition definition)
        {
            return definition.HasMaxBalance ? definition.MaxBalance : int.MaxValue;
        }

        private ResourceBalance FindSaved(string resourceId)
        {
            return store.Data.balances.Find(entry => entry.resourceId == resourceId);
        }

        private void WriteBalance(string resourceId, int balance)
        {
            var saved = FindSaved(resourceId);
            if (saved != null)
                saved.balance = balance;
            else
                store.Data.balances.Add(new ResourceBalance { resourceId = resourceId, balance = balance });
        }

        // Saved before BalanceChanged is raised, so a listener that throws can't stop the new balance
        // from reaching disk.
        private void Persist(string resourceId)
        {
            WriteBalance(resourceId, balances[resourceId]);
            store.Save();
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

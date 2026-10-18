using System;
using System.Collections.Generic;

namespace RewardClaims
{
    /// <summary><see cref="IStorage"/> kept in memory only; nothing survives the process. Meant for tests.</summary>
    /// <remarks>
    /// Lives in Runtime (not in the test assembly) so games can use it in their own tests too.
    /// Reusing the same instance simulates an app restart: build new SDK objects on top of it.
    /// </remarks>
    public class InMemoryStorage : IStorage
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        /// <inheritdoc/>
        public string Load(string key)
        {
            EnsureValidKey(key);
            return values.TryGetValue(key, out var value) ? value : null;
        }

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            EnsureValidKey(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            values[key] = value;
        }

        private static void EnsureValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Storage key must not be null or empty.", nameof(key));
        }
    }
}

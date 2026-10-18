using System;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>Default <see cref="IStorage"/> backed by Unity's PlayerPrefs.</summary>
    public class PlayerPrefsStorage : IStorage
    {
        /// <inheritdoc/>
        public string Load(string key)
        {
            EnsureValidKey(key);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            EnsureValidKey(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            PlayerPrefs.SetString(key, value);
            // Flush now instead of waiting for Unity's save on quit: a crash or a force-kill from the
            // OS never reaches that point, and the player would lose rewards they already earned.
            PlayerPrefs.Save();
        }

        private static void EnsureValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Storage key must not be null or empty.", nameof(key));
        }
    }
}

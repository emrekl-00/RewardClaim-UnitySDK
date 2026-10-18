using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewardClaims
{
    // Loads one player's PlayerProgress from IStorage as JSON and writes it back on Save().
    internal class ProgressStore
    {
        private const string KeyPrefix = "RewardClaims.Progress.";

        private readonly IStorage storage;
        private readonly string key;

        // The in-memory document; change it, then call Save().
        public PlayerProgress Data { get; }

        public ProgressStore(IStorage storage, string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player id must not be null or empty.", nameof(playerId));

            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            key = KeyPrefix + playerId;
            Data = Load();
        }

        public void Save()
        {
            storage.Save(key, JsonUtility.ToJson(Data));
        }

        private PlayerProgress Load()
        {
            var json = storage.Load(key);
            if (json == null)
                return new PlayerProgress();

            try
            {
                var progress = JsonUtility.FromJson<PlayerProgress>(json);
                if (progress != null)
                {
                    // Valid JSON without a "balances" field (e.g. "{}") must still give a usable list.
                    if (progress.balances == null)
                        progress.balances = new List<ResourceBalance>();
                    return progress;
                }
            }
            catch (ArgumentException)
            {
                // JsonUtility throws ArgumentException for malformed JSON; handled below like a null result.
            }

            // A broken save must not crash the game on startup. Starting fresh loses this player's
            // progress, so warn loudly enough that it shows up in logs and crash reports.
            Debug.LogWarning($"[RewardClaims] Saved progress under '{key}' could not be read. Starting with fresh progress.");
            return new PlayerProgress();
        }
    }
}

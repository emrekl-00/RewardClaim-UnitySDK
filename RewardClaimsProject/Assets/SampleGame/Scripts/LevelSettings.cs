using System;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>Difficulty values for one level, edited in the Inspector on a <see cref="LevelList"/>.</summary>
    [Serializable]
    public class LevelSettings
    {
        [Tooltip("Coins to catch to complete the level.")]
        [Min(1)] [SerializeField] private int targetCoins = 5;

        [Tooltip("Coins the player may miss. One more miss ends the run.")]
        [Min(0)] [SerializeField] private int allowedMisses = 3;

        [Tooltip("How fast coins fall, in world units per second.")]
        [Min(0.1f)] [SerializeField] private float fallSpeed = 3f;

        [Tooltip("Seconds between two coins.")]
        [Min(0.1f)] [SerializeField] private float spawnInterval = 1f;

        public int TargetCoins => targetCoins;
        public int AllowedMisses => allowedMisses;
        public float FallSpeed => fallSpeed;
        public float SpawnInterval => spawnInterval;
    }
}

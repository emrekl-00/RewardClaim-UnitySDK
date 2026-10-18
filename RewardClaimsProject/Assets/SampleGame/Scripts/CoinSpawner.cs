using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>Drops coins at random x positions along the top of the screen while a level is running.</summary>
    public class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Coin coinPrefab;
        [Tooltip("Keeps coins this far from the screen edges, in world units.")]
        [SerializeField] private float edgeMargin = 0.4f;

        private readonly List<Coin> activeCoins = new List<Coin>();
        private LevelSettings level;
        private float timeUntilNextCoin;

        /// <summary>Raised when the basket catches a coin.</summary>
        public event Action CoinCaught;

        /// <summary>Raised when a coin falls past the bottom of the screen.</summary>
        public event Action CoinMissed;

        public bool IsSpawning => level != null;

        /// <summary>Starts dropping coins with this level's speed and interval.</summary>
        public void Begin(LevelSettings levelSettings)
        {
            Clear();
            level = levelSettings;
            timeUntilNextCoin = 0f; // first coin right away, so the player isn't waiting
        }

        /// <summary>Stops spawning and removes coins still in the air.</summary>
        public void Stop()
        {
            level = null;
            Clear();
        }

        private void Update()
        {
            if (!IsSpawning)
                return;

            // Count down in real seconds; deltaTime keeps the interval the same at any frame rate.
            timeUntilNextCoin -= Time.deltaTime;
            if (timeUntilNextCoin > 0f)
                return;

            SpawnCoin();
            timeUntilNextCoin += level.SpawnInterval;
        }

        private void SpawnCoin()
        {
            var x = UnityEngine.Random.Range(CameraBounds.Left(gameCamera) + edgeMargin,
                                             CameraBounds.Right(gameCamera) - edgeMargin);
            var y = CameraBounds.Top(gameCamera) + edgeMargin; // just above the screen, so it slides in

            var coin = Instantiate(coinPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
            activeCoins.Add(coin);
            coin.Launch(level.FallSpeed, CameraBounds.Bottom(gameCamera) - edgeMargin, OnCaught, OnMissed);
        }

        private void OnCaught(Coin coin)
        {
            activeCoins.Remove(coin);
            CoinCaught?.Invoke();
        }

        private void OnMissed(Coin coin)
        {
            activeCoins.Remove(coin);
            CoinMissed?.Invoke();
        }

        private void Clear()
        {
            foreach (var coin in activeCoins)
                Destroy(coin.gameObject);

            activeCoins.Clear();
        }
    }
}

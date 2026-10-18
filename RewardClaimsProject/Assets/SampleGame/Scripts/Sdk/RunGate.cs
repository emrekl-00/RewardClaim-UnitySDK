using System;
using RewardClaims;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// The one place a run starts from: it charges a life through the SDK and only then asks the
    /// <see cref="GameManager"/> to start. Play, Retry, Replay and Next all come through here.
    /// </summary>
    public class RunGate : MonoBehaviour
    {
        private const int LivesPerRun = 1;

        [SerializeField] private GameManager game;

        /// <summary>Raised when a run was refused because the player has no lives left.</summary>
        public event Action NoLivesLeft;

        /// <summary>Starts (or restarts) the current level if a life can be paid. Used by Play, Retry and Replay.</summary>
        public void StartLevel()
        {
            // Check the game's own rule first: a life spent on a start the game then ignores
            // (e.g. a double click while a run is already going) would simply be lost.
            if (game.CanStartLevel && TryPayForRun())
                game.StartLevel();
        }

        /// <summary>Starts the next level if a life can be paid. Used by Next.</summary>
        public void StartNextLevel()
        {
            if (game.CanStartNextLevel && TryPayForRun())
                game.StartNextLevel();
        }

        private bool TryPayForRun()
        {
            // Not enough lives is a normal game situation, so TrySpend returns false instead of throwing.
            if (RewardClaimsSdk.Wallet.TrySpend(ResourceIds.Lives, LivesPerRun))
                return true;

            NoLivesLeft?.Invoke();
            return false;
        }
    }
}

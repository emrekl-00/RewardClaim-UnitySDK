using System;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// Runs the game loop: Ready → Playing → LevelComplete or GameOver, and counts caught and missed coins.
    /// A "run" is one attempt at a level. Other scripts (the HUD now, the SDK integration later) only
    /// react to its events; they never change the score themselves.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelList levels;
        [SerializeField] private CoinSpawner spawner;
        [SerializeField] private Basket basket;

        /// <summary>Raised whenever <see cref="State"/> changes.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Raised whenever <see cref="Caught"/> or <see cref="Missed"/> changes.</summary>
        public event Action ScoreChanged;

        /// <summary>Raised when a run starts, with the level index.</summary>
        public event Action<int> RunStarted;

        /// <summary>Raised when the player completes a level, with the level index.</summary>
        public event Action<int> LevelCompleted;

        /// <summary>Raised when a run ends, with the level index and whether the level was completed.</summary>
        public event Action<int, bool> RunEnded;

        public GameState State { get; private set; } = GameState.Ready;

        /// <summary>Zero-based index of the level being played (shown to players as index + 1).</summary>
        public int LevelIndex { get; private set; }

        public LevelSettings CurrentLevel => levels[LevelIndex];
        public bool HasNextLevel => LevelIndex + 1 < levels.Count;
        public int Caught { get; private set; }
        public int Missed { get; private set; }

        private void Awake()
        {
            // The basket only takes input during a run.
            basket.enabled = false;
        }

        // Subscribe in OnEnable and unsubscribe in OnDisable, so a disabled manager never reacts.
        private void OnEnable()
        {
            spawner.CoinCaught += OnCoinCaught;
            spawner.CoinMissed += OnCoinMissed;
        }

        private void OnDisable()
        {
            spawner.CoinCaught -= OnCoinCaught;
            spawner.CoinMissed -= OnCoinMissed;
        }

        /// <summary>Starts (or restarts) a run of the current level. Used by Play, Retry and Replay.</summary>
        public void StartLevel()
        {
            if (State == GameState.Playing)
                return;

            Caught = 0;
            Missed = 0;
            basket.Recenter();
            basket.enabled = true;
            spawner.Begin(CurrentLevel);

            SetState(GameState.Playing);
            ScoreChanged?.Invoke();
            RunStarted?.Invoke(LevelIndex);
        }

        /// <summary>Moves on to the next level and starts it. Only valid after completing a level.</summary>
        public void StartNextLevel()
        {
            if (State != GameState.LevelComplete || !HasNextLevel)
                return;

            LevelIndex++;
            StartLevel();
        }

        private void OnCoinCaught()
        {
            if (State != GameState.Playing)
                return;

            Caught++;
            ScoreChanged?.Invoke();

            if (Caught >= CurrentLevel.TargetCoins)
                EndRun(completed: true);
        }

        private void OnCoinMissed()
        {
            if (State != GameState.Playing)
                return;

            Missed++;
            ScoreChanged?.Invoke();

            // The allowed misses are a budget: using all of them is fine, one more ends the run.
            if (Missed > CurrentLevel.AllowedMisses)
                EndRun(completed: false);
        }

        private void EndRun(bool completed)
        {
            spawner.Stop();
            basket.enabled = false;

            SetState(completed ? GameState.LevelComplete : GameState.GameOver);
            if (completed)
                LevelCompleted?.Invoke(LevelIndex);
            RunEnded?.Invoke(LevelIndex, completed);
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}

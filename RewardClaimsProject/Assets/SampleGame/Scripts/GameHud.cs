using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// Shows the level, the score and one panel per game state, and turns button clicks into
    /// run starts through the <see cref="RunGate"/>. It only displays state; the GameManager owns it.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [Tooltip("Every button that starts a run goes through here, so each run costs a life.")]
        [SerializeField] private RunGate runGate;

        [Header("Always visible")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text scoreLabel;

        [Header("Ready")]
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private TMP_Text goalLabel;
        [SerializeField] private Button playButton;

        [Header("Level complete")]
        [SerializeField] private GameObject levelCompletePanel;
        [SerializeField] private TMP_Text levelCompleteTitle;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button replayButton;

        [Header("Game over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button retryButton;

        private void Awake()
        {
            // Wired in code rather than in each Button's OnClick list, so every click handler is
            // readable in one file. The buttons call the RunGate, not the GameManager: a run has to
            // be paid for with a life first.
            playButton.onClick.AddListener(runGate.StartLevel);
            replayButton.onClick.AddListener(runGate.StartLevel);
            retryButton.onClick.AddListener(runGate.StartLevel);
            nextButton.onClick.AddListener(runGate.StartNextLevel);
        }

        private void OnEnable()
        {
            game.StateChanged += OnStateChanged;
            game.ScoreChanged += Refresh;
        }

        private void OnDisable()
        {
            game.StateChanged -= OnStateChanged;
            game.ScoreChanged -= Refresh;
        }

        // Start runs after every object's Awake, so the GameManager is fully set up by now.
        private void Start()
        {
            Refresh();
        }

        private void OnStateChanged(GameState state)
        {
            Refresh();
        }

        private void Refresh()
        {
            var level = game.CurrentLevel;
            var state = game.State;

            levelLabel.text = $"Level {game.LevelIndex + 1}";
            scoreLabel.text = $"Caught {game.Caught}/{level.TargetCoins}  ·  Missed {game.Missed}/{level.AllowedMisses}";
            goalLabel.text = $"Catch {level.TargetCoins} coins.\nMiss more than {level.AllowedMisses} and it's over.";

            readyPanel.SetActive(state == GameState.Ready);
            levelCompletePanel.SetActive(state == GameState.LevelComplete);
            gameOverPanel.SetActive(state == GameState.GameOver);

            levelCompleteTitle.text = game.HasNextLevel ? "Level complete!" : "All levels complete!";
            nextButton.gameObject.SetActive(game.HasNextLevel);
        }
    }
}

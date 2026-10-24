using System.Collections;
using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // After GAME OVER has shown for GameRulesConfig.gameOverSeconds, offers Retry or Main Menu
    // with the final score, and a notice when the run beat the high score it started against.
    public sealed class GameOverView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;
        [SerializeField] UiConfig ui;

        [Header("Widgets")]
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text finalScoreText;
        [SerializeField] TMP_Text newHighScoreText;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainMenuButton;

        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] VoidEventChannel newGameStarted;
        [SerializeField] IntEventChannel scoreChanged;
        [SerializeField] IntEventChannel highScoreChanged;

        [Header("Raises")]
        [SerializeField] VoidEventChannel restartRequested;
        [SerializeField] VoidEventChannel mainMenuRequested;

        int score;
        int highScore;
        int highScoreAtStart;

        void Awake()
        {
            title.text = ui.MessageFor(GameState.GameOver);
            newHighScoreText.text = ui.NewHighScoreText;
            panel.SetActive(false);
        }

        void OnEnable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised += OnGameStateChanged;
            if (newGameStarted != null) newGameStarted.Raised += OnNewGame;
            if (scoreChanged != null) scoreChanged.Raised += OnScoreChanged;
            if (highScoreChanged != null) highScoreChanged.Raised += OnHighScoreChanged;
            retryButton.onClick.AddListener(Retry);
            mainMenuButton.onClick.AddListener(MainMenu);
        }

        void OnDisable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (newGameStarted != null) newGameStarted.Raised -= OnNewGame;
            if (scoreChanged != null) scoreChanged.Raised -= OnScoreChanged;
            if (highScoreChanged != null) highScoreChanged.Raised -= OnHighScoreChanged;
            retryButton.onClick.RemoveListener(Retry);
            mainMenuButton.onClick.RemoveListener(MainMenu);
        }

        void OnGameStateChanged(GameState state)
        {
            StopAllCoroutines();

            if (state == GameState.GameOver)
                StartCoroutine(ShowAfterDelay());
            else
                panel.SetActive(false);
        }

        IEnumerator ShowAfterDelay()
        {
            if (rules.GameOverSeconds > 0f)
                yield return new WaitForSecondsRealtime(rules.GameOverSeconds);

            finalScoreText.text = string.Format(ui.FinalScoreFormat, score);
            newHighScoreText.gameObject.SetActive(score > highScoreAtStart);
            panel.SetActive(true);
        }

        void OnNewGame()
        {
            score = 0;
            highScoreAtStart = highScore;
        }

        void OnScoreChanged(int value) => score = value;

        // HighScoreSystem announces the saved score before GameManager starts the first game
        // (it runs earlier in the execution order), so OnNewGame always sees the real baseline.
        void OnHighScoreChanged(int value) => highScore = value;

        void Retry() => restartRequested.Raise();

        void MainMenu() => mainMenuRequested.Raise();
    }
}

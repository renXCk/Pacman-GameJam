using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // The in-game HUD. It reads nothing from gameplay objects: every number arrives as an event
    // payload, and every word comes from UiConfig. It only redraws when something changes.
    public sealed class HudView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] UiConfig ui;

        [Header("Widgets")]
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text highScoreText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text livesLabel;
        [Tooltip("One icon per life, up to GameRulesConfig.maxDisplayedLives.")]
        [SerializeField] Image[] lifeIcons;
        [SerializeField] TMP_Text batteryLabel;
        [SerializeField] Image batteryFill;
        [SerializeField] TMP_Text messageText;

        [Header("Listens to")]
        [SerializeField] IntEventChannel scoreChanged;
        [SerializeField] IntEventChannel highScoreChanged;
        [SerializeField] IntEventChannel livesChanged;
        [SerializeField] IntEventChannel levelNumberChanged;
        [SerializeField] FloatEventChannel batteryChanged;
        [SerializeField] GameStateEventChannel gameStateChanged;

        void Awake()
        {
            if (livesLabel != null) livesLabel.text = ui.LivesLabel;
            if (batteryLabel != null) batteryLabel.text = ui.BatteryLabel;
            ShowScore(0);
            ShowHighScore(0);
            ShowLevel(1);
            ShowBattery(1f);
            ShowState(GameState.Boot);
        }

        void OnEnable()
        {
            if (scoreChanged != null) scoreChanged.Raised += ShowScore;
            if (highScoreChanged != null) highScoreChanged.Raised += ShowHighScore;
            if (livesChanged != null) livesChanged.Raised += ShowLives;
            if (levelNumberChanged != null) levelNumberChanged.Raised += ShowLevel;
            if (batteryChanged != null) batteryChanged.Raised += ShowBattery;
            if (gameStateChanged != null) gameStateChanged.Raised += ShowState;
        }

        void OnDisable()
        {
            if (scoreChanged != null) scoreChanged.Raised -= ShowScore;
            if (highScoreChanged != null) highScoreChanged.Raised -= ShowHighScore;
            if (livesChanged != null) livesChanged.Raised -= ShowLives;
            if (levelNumberChanged != null) levelNumberChanged.Raised -= ShowLevel;
            if (batteryChanged != null) batteryChanged.Raised -= ShowBattery;
            if (gameStateChanged != null) gameStateChanged.Raised -= ShowState;
        }

        void ShowScore(int score) => scoreText.text = string.Format(ui.ScoreFormat, score);

        void ShowHighScore(int score) => highScoreText.text = string.Format(ui.HighScoreFormat, score);

        void ShowLevel(int level) => levelText.text = string.Format(ui.LevelFormat, level);

        void ShowLives(int lives)
        {
            for (int i = 0; i < lifeIcons.Length; i++)
                lifeIcons[i].enabled = i < lives;
        }

        void ShowBattery(float charge) => batteryFill.fillAmount = Mathf.Clamp01(charge);

        void ShowState(GameState state)
        {
            string message = ui.MessageFor(state);
            messageText.text = message;
            messageText.enabled = message.Length > 0;
        }
    }
}

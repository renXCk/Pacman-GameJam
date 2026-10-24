using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // Main menu: Play, Controls, Settings, Quit, and the saved high score (heard from
    // HighScoreSystem on HighScoreChanged). Scene names come from GameRulesConfig.
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;
        [SerializeField] UiConfig ui;

        [Header("Widgets")]
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text highScoreText;
        [SerializeField] Button playButton;
        [SerializeField] Button controlsButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] ControlsView controls;
        [SerializeField] SettingsView settings;

        [Header("Listens to")]
        [SerializeField] IntEventChannel highScoreChanged;

        void Awake()
        {
            title.text = ui.GameTitle;
            ShowHighScore(0);
        }

        void OnEnable()
        {
            if (highScoreChanged != null) highScoreChanged.Raised += ShowHighScore;
            playButton.onClick.AddListener(Play);
            controlsButton.onClick.AddListener(controls.Show);
            settingsButton.onClick.AddListener(settings.Show);
            quitButton.onClick.AddListener(Quit);
        }

        void OnDisable()
        {
            if (highScoreChanged != null) highScoreChanged.Raised -= ShowHighScore;
            playButton.onClick.RemoveListener(Play);
            controlsButton.onClick.RemoveListener(controls.Show);
            settingsButton.onClick.RemoveListener(settings.Show);
            quitButton.onClick.RemoveListener(Quit);
        }

        void ShowHighScore(int score) => highScoreText.text = string.Format(ui.HighScoreFormat, score);

        void Play() => SceneManager.LoadScene(rules.GameScene);

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

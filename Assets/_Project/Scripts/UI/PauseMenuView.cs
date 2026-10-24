using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Shows itself when PauseChanged says paused. Its buttons only ask, through channels:
    // Resume raises PauseRequested(false), Main Menu raises MainMenuRequested.
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] UiConfig ui;

        [Header("Widgets")]
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] Button resumeButton;
        [SerializeField] Button controlsButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] ControlsView controls;

        [Header("Listens to")]
        [SerializeField] BoolEventChannel pauseChanged;

        [Header("Raises")]
        [SerializeField] BoolEventChannel pauseRequested;
        [SerializeField] VoidEventChannel mainMenuRequested;

        void Awake()
        {
            title.text = ui.PausedTitle;
            panel.SetActive(false);
        }

        void OnEnable()
        {
            if (pauseChanged != null) pauseChanged.Raised += OnPauseChanged;
            resumeButton.onClick.AddListener(Resume);
            controlsButton.onClick.AddListener(controls.Show);
            mainMenuButton.onClick.AddListener(MainMenu);
        }

        void OnDisable()
        {
            if (pauseChanged != null) pauseChanged.Raised -= OnPauseChanged;
            resumeButton.onClick.RemoveListener(Resume);
            controlsButton.onClick.RemoveListener(controls.Show);
            mainMenuButton.onClick.RemoveListener(MainMenu);
        }

        void OnPauseChanged(bool paused)
        {
            panel.SetActive(paused);
            if (!paused)
                controls.Hide();
        }

        void Resume() => pauseRequested.Raise(false);

        void MainMenu() => mainMenuRequested.Raise();
    }
}

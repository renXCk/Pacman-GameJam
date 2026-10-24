using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // The controls screen, shared by the main menu and the pause menu. The list comes from
    // UiConfig so it can be corrected without opening a scene.
    public sealed class ControlsView : MonoBehaviour
    {
        [SerializeField] UiConfig ui;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] Button backButton;

        void Awake()
        {
            title.text = ui.ControlsTitle;
            body.text = string.Join("\n", ui.ControlLines);
            panel.SetActive(false);
        }

        void OnEnable() => backButton.onClick.AddListener(Hide);

        void OnDisable() => backButton.onClick.RemoveListener(Hide);

        public void Show() => panel.SetActive(true);

        public void Hide() => panel.SetActive(false);
    }
}

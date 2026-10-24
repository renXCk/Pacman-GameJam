using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Settings screen: master volume for now.
    public sealed class SettingsView : MonoBehaviour
    {
        [SerializeField] UiConfig ui;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text volumeLabel;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Button backButton;

        void Awake()
        {
            title.text = ui.SettingsTitle;
            volumeLabel.text = ui.VolumeLabel;
            panel.SetActive(false);
        }

        void OnEnable()
        {
            volumeSlider.SetValueWithoutNotify(VolumeSettings.Load(ui));
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            backButton.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            backButton.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            volumeSlider.SetValueWithoutNotify(VolumeSettings.Load(ui));
            panel.SetActive(true);
        }

        public void Hide() => panel.SetActive(false);

        void OnVolumeChanged(float value) => VolumeSettings.Save(ui, value);
    }
}

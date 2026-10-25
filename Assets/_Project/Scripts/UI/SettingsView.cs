using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Settings screen: master volume and the Screen Effects toggle. A toggle change is saved and
    // announced on ScreenEffectsChanged, so the CRT overlay and flickering text react at once.
    public sealed class SettingsView : MonoBehaviour
    {
        [SerializeField] UiConfig ui;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text volumeLabel;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text effectsLabel;
        [SerializeField] Button effectsButton;
        [SerializeField] TMP_Text effectsValue;
        [SerializeField] Button backButton;

        [Header("Raises")]
        [SerializeField] BoolEventChannel screenEffectsChanged;

        void Awake()
        {
            title.text = ui.SettingsTitle;
            volumeLabel.text = ui.VolumeLabel;
            effectsLabel.text = ui.EffectsLabel;
            panel.SetActive(false);
        }

        void OnEnable()
        {
            volumeSlider.SetValueWithoutNotify(VolumeSettings.Load(ui));
            ShowEffects(ScreenEffectsSettings.IsOn(ui));
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            effectsButton.onClick.AddListener(ToggleEffects);
            backButton.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            effectsButton.onClick.RemoveListener(ToggleEffects);
            backButton.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            volumeSlider.SetValueWithoutNotify(VolumeSettings.Load(ui));
            ShowEffects(ScreenEffectsSettings.IsOn(ui));
            panel.SetActive(true);
        }

        public void Hide() => panel.SetActive(false);

        void OnVolumeChanged(float value) => VolumeSettings.Save(ui, value);

        void ToggleEffects()
        {
            bool on = !ScreenEffectsSettings.IsOn(ui);
            ScreenEffectsSettings.Save(ui, on);
            ShowEffects(on);

            if (screenEffectsChanged != null)
                screenEffectsChanged.Raise(on);
        }

        void ShowEffects(bool on) => effectsValue.text = on ? ui.OnText : ui.OffText;
    }
}

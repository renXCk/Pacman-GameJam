using Game.Core;
using UnityEngine;

namespace Game.UI
{
    // Applies the saved master volume when a scene starts, so the Game scene respects the setting
    // even when it is played directly without going through the menu.
    public sealed class AudioSettingsLoader : MonoBehaviour
    {
        [SerializeField] UiConfig ui;

        void Awake() => VolumeSettings.ApplySaved(ui);
    }
}

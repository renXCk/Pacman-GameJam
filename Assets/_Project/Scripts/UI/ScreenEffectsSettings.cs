using Game.Core;
using UnityEngine;

namespace Game.UI
{
    // The player's Screen Effects setting (scanlines, grain, vignette, flicker), saved in
    // PlayerPrefs. Stateless like VolumeSettings: the saved value is the only state.
    public static class ScreenEffectsSettings
    {
        public static bool IsOn(UiConfig ui) => PlayerPrefs.GetInt(ui.EffectsPrefsKey, ui.EffectsOnByDefault ? 1 : 0) != 0;

        public static void Save(UiConfig ui, bool on)
        {
            PlayerPrefs.SetInt(ui.EffectsPrefsKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

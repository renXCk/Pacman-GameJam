using Game.Core;
using UnityEngine;

namespace Game.UI
{
    // Master volume, saved in PlayerPrefs. Stateless on purpose: the saved value is the only
    // state. Dev D's AudioManager can build on the same key from UiConfig.
    public static class VolumeSettings
    {
        public static float Load(UiConfig ui) => Mathf.Clamp01(PlayerPrefs.GetFloat(ui.VolumePrefsKey, ui.DefaultVolume));

        public static void Save(UiConfig ui, float volume)
        {
            volume = Mathf.Clamp01(volume);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(ui.VolumePrefsKey, volume);
            PlayerPrefs.Save();
        }

        public static void ApplySaved(UiConfig ui) => AudioListener.volume = Load(ui);
    }
}

using UnityEngine;

namespace Game.Core
{
    // Every piece of UI text that is really data: the title, state messages, labels, the
    // controls list and the volume setting. Kept in one asset so wording can change (or be
    // translated) without touching a scene or a script.
    [CreateAssetMenu(fileName = "UiConfig", menuName = "Game/Configs/UI Config")]
    public sealed class UiConfig : ScriptableObject
    {
        [Header("Title")]
        [Tooltip("Game name on the main menu. Working title until the team decides.")]
        [SerializeField] string gameTitle = "BLACKOUT MAZE";

        [Header("State messages (HUD center)")]
        [SerializeField] string readyMessage = "READY";
        [SerializeField] string caughtMessage = "CAUGHT";
        [SerializeField] string levelCompleteMessage = "LEVEL COMPLETE";
        [SerializeField] string gameOverMessage = "GAME OVER";

        [Header("HUD formats ({0} is the value)")]
        [SerializeField] string scoreFormat = "SCORE  {0}";
        [SerializeField] string highScoreFormat = "HIGH SCORE  {0}";
        [SerializeField] string levelFormat = "LEVEL {0}";
        [SerializeField] string livesLabel = "LIVES";
        [SerializeField] string batteryLabel = "BATTERY";

        [Header("Screens")]
        [SerializeField] string pausedTitle = "PAUSED";
        [SerializeField] string finalScoreFormat = "FINAL SCORE  {0}";
        [SerializeField] string newHighScoreText = "NEW HIGH SCORE";
        [SerializeField] string controlsTitle = "CONTROLS";
        [SerializeField] string settingsTitle = "SETTINGS";
        [SerializeField] string volumeLabel = "MASTER VOLUME";

        [Header("Controls screen")]
        [Tooltip("One line per control. Draft: confirm with Dev A once the player controller exists.")]
        [SerializeField] string[] controlLines =
        {
            "WASD / Arrow keys    Move",
            "Mouse                Look",
            "Left click           Toggle flashlight",
            "Esc                  Pause"
        };

        [Header("Settings")]
        [Tooltip("PlayerPrefs key for the master volume. Prefixed so other settings cannot collide with it.")]
        [SerializeField] string volumePrefsKey = "audio.masterVolume";

        [Tooltip("Master volume (0 to 1) before the player changes it.")]
        [Range(0f, 1f)] [SerializeField] float defaultVolume = 0.8f;

        public string GameTitle => gameTitle;
        public string ScoreFormat => scoreFormat;
        public string HighScoreFormat => highScoreFormat;
        public string LevelFormat => levelFormat;
        public string LivesLabel => livesLabel;
        public string BatteryLabel => batteryLabel;
        public string PausedTitle => pausedTitle;
        public string FinalScoreFormat => finalScoreFormat;
        public string NewHighScoreText => newHighScoreText;
        public string ControlsTitle => controlsTitle;
        public string SettingsTitle => settingsTitle;
        public string VolumeLabel => volumeLabel;
        public string[] ControlLines => controlLines;
        public string VolumePrefsKey => volumePrefsKey;
        public float DefaultVolume => defaultVolume;

        // The HUD's center message for a state. Empty while playing, so nothing covers the maze.
        public string MessageFor(GameState state) => state switch
        {
            GameState.Ready => readyMessage,
            GameState.PlayerCaught => caughtMessage,
            GameState.LevelComplete => levelCompleteMessage,
            GameState.GameOver => gameOverMessage,
            _ => ""
        };
    }
}

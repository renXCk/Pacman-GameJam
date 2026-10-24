using UnityEngine;

namespace Game.Core
{
    // The rules of one game: lives, the extra life, how long each GameManager state lasts,
    // and the scenes it moves between. One asset for the whole game.
    [CreateAssetMenu(fileName = "GameRulesConfig", menuName = "Game/Configs/Game Rules Config")]
    public sealed class GameRulesConfig : ScriptableObject
    {
        [Header("Lives")]
        [Tooltip("Lives at the start of a new game.")]
        [Min(1)] [SerializeField] int startingLives = 3;

        [Tooltip("Score that awards one extra life. Awarded once per game, as in the original. 0 turns it off.")]
        [Min(0)] [SerializeField] int extraLifeScore = 10000;

        [Tooltip("Most life icons the HUD draws. Lives above this still count.")]
        [Min(1)] [SerializeField] int maxDisplayedLives = 5;

        [Header("State timings (seconds)")]
        [Tooltip("Ready: the player and ghosts wait at their start points before Playing.")]
        [Min(0f)] [SerializeField] float readySeconds = 2f;

        [Tooltip("PlayerCaught: the caught moment before the next Ready (or Game Over).")]
        [Min(0f)] [SerializeField] float caughtSeconds = 2f;

        [Tooltip("LevelComplete: the pause before the next, harder level starts.")]
        [Min(0f)] [SerializeField] float levelCompleteSeconds = 3f;

        [Tooltip("GameOver: how long GAME OVER shows before the Retry / Main Menu screen appears.")]
        [Min(0f)] [SerializeField] float gameOverSeconds = 3f;

        [Header("Scenes")]
        [Tooltip("Scene loaded by Main Menu buttons. Must be in the build settings.")]
        [SerializeField] string menuScene = "MainMenu";

        [Tooltip("Scene loaded by Play. Must be in the build settings.")]
        [SerializeField] string gameScene = "Game";

        [Header("Save")]
        [Tooltip("High score file, inside Application.persistentDataPath.")]
        [SerializeField] string highScoreFile = "highscore.json";

        public int StartingLives => startingLives;
        public int ExtraLifeScore => extraLifeScore;
        public int MaxDisplayedLives => maxDisplayedLives;
        public float ReadySeconds => readySeconds;
        public float CaughtSeconds => caughtSeconds;
        public float LevelCompleteSeconds => levelCompleteSeconds;
        public float GameOverSeconds => gameOverSeconds;
        public string MenuScene => menuScene;
        public string GameScene => gameScene;
        public string HighScoreFile => highScoreFile;
    }
}

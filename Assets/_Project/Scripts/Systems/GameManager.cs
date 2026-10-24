using System.Collections;
using Game.Core;
using Game.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems
{
    // Runs the game loop: Ready, Playing, PlayerCaught, LevelComplete, GameOver. It never touches
    // the player, the ghosts or the UI. It raises GameStateChanged and LevelStarted, and everyone
    // else reacts; it hears about catches, lives and the last cell through channels as well.
    //
    // Order on every level start (the ghosts depend on it): LevelNumberChanged, LevelStarted(config),
    // GameStateChanged(Ready), wait, GameStateChanged(Playing).
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;
        [SerializeField] LevelSequence levels;
        [Tooltip("The maze counts its cells here; the last one collected completes the level.")]
        [SerializeField] LevelProgress progress;

        [Header("Listens to")]
        [SerializeField] VoidEventChannel playerCaught;
        [SerializeField] IntEventChannel livesChanged;
        [SerializeField] VoidEventChannel restartRequested;
        [SerializeField] VoidEventChannel mainMenuRequested;

        [Header("Raises")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] IntEventChannel levelNumberChanged;
        [SerializeField] VoidEventChannel levelComplete;
        [SerializeField] VoidEventChannel newGameStarted;
        [SerializeField] VoidEventChannel lifeLost;

        [Header("Startup")]
        [Tooltip("Start a game as soon as the scene loads, so the Game scene can be played without the menu.")]
        [SerializeField] bool autoStart = true;

        GameState state = GameState.Boot;
        int levelIndex;
        int lives;

        public GameState State => state;
        public int LevelIndex => levelIndex;
        public int Lives => lives;

        void OnEnable()
        {
            if (!IsConfigured())
            {
                enabled = false;
                return;
            }

            if (playerCaught != null) playerCaught.Raised += OnPlayerCaught;
            if (livesChanged != null) livesChanged.Raised += OnLivesChanged;
            if (restartRequested != null) restartRequested.Raised += BeginGame;
            if (mainMenuRequested != null) mainMenuRequested.Raised += GoToMenu;
            progress.Completed += OnLevelCleared;
        }

        void OnDisable()
        {
            if (playerCaught != null) playerCaught.Raised -= OnPlayerCaught;
            if (livesChanged != null) livesChanged.Raised -= OnLivesChanged;
            if (restartRequested != null) restartRequested.Raised -= BeginGame;
            if (mainMenuRequested != null) mainMenuRequested.Raised -= GoToMenu;
            if (progress != null) progress.Completed -= OnLevelCleared;
        }

        void Start()
        {
            if (autoStart && enabled)
                BeginGame();
        }

        public void BeginGame()
        {
            StopAllCoroutines();

            // LivesSystem answers NewGameStarted with LivesChanged; this is only the fallback.
            lives = rules.StartingLives;
            if (newGameStarted != null)
                newGameStarted.Raise();

            StartCoroutine(BeginLevel(0));
        }

        IEnumerator BeginLevel(int index)
        {
            levelIndex = index;
            if (levelNumberChanged != null)
                levelNumberChanged.Raise(index + 1);

            levelStarted.Raise(levels.Get(index));
            yield return ReadyThenPlay();
        }

        IEnumerator ReadyThenPlay()
        {
            SetState(GameState.Ready);
            yield return Wait(rules.ReadySeconds);
            SetState(GameState.Playing);
        }

        void OnPlayerCaught()
        {
            // The ghosts raise this once per catch; anything outside Playing is a leftover.
            if (state != GameState.Playing)
                return;

            StopAllCoroutines();
            StartCoroutine(Caught());
        }

        IEnumerator Caught()
        {
            SetState(GameState.PlayerCaught);
            LoseLife();

            yield return Wait(rules.CaughtSeconds);

            // Collected cells stay collected: only the ghosts and the player go back to start.
            if (lives > 0)
            {
                yield return ReadyThenPlay();
                yield break;
            }

            // HighScoreSystem saves on GameOver; the game over screen offers Retry or Main Menu.
            SetState(GameState.GameOver);
        }

        void OnLevelCleared()
        {
            if (state != GameState.Playing)
                return;

            StopAllCoroutines();
            StartCoroutine(LevelCleared());
        }

        IEnumerator LevelCleared()
        {
            SetState(GameState.LevelComplete);
            if (levelComplete != null)
                levelComplete.Raise();

            yield return Wait(rules.LevelCompleteSeconds);
            yield return BeginLevel(levelIndex + 1);
        }

        // LivesSystem hears LifeLost and answers on LivesChanged before Raise returns.
        void LoseLife()
        {
            int before = lives;
            if (lifeLost != null)
                lifeLost.Raise();

            if (lives != before || lives == 0)
                return;

            // Nobody answered: keep the game finite anyway, and say why the HUD did not change.
            Debug.LogWarning($"{name}: no LivesSystem answered LifeLost. Counting lives locally.", this);
            lives--;
        }

        void OnLivesChanged(int value) => lives = value;

        void GoToMenu()
        {
            StopAllCoroutines();
            SetState(GameState.Menu);
            SceneManager.LoadScene(rules.MenuScene);
        }

        void SetState(GameState next)
        {
            state = next;
            gameStateChanged.Raise(next);
        }

        static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
        }

        // For testing the flow in a scene without ghosts or a maze.
        [ContextMenu("Debug/Catch Player")]
        void DebugCatchPlayer()
        {
            if (playerCaught != null)
                playerCaught.Raise();
        }

        [ContextMenu("Debug/Complete Level")]
        void DebugCompleteLevel() => OnLevelCleared();

        bool IsConfigured()
        {
            string missing = "";
            if (rules == null) missing += " GameRulesConfig";
            if (levels == null || levels.Count == 0) missing += " LevelSequence";
            if (progress == null) missing += " LevelProgress";
            if (gameStateChanged == null) missing += " GameStateChanged channel";
            if (levelStarted == null) missing += " LevelStarted channel";

            if (missing.Length == 0)
                return true;

            Debug.LogError($"{name} is missing:{missing}. The game will not start.", this);
            return false;
        }
    }
}

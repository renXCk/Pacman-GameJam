using System.IO;
using Game.Core;
using Game.Events;
using UnityEngine;

namespace Game.Systems
{
    // Loads the saved high score, announces it, raises it live when the score beats it, and
    // writes it to disk at game over or when leaving the game. It writes on those moments rather
    // than on every pickup, so a run of points never costs a file write per frame.
    // Runs before GameManager so the saved score is announced before the first NewGameStarted.
    [DefaultExecutionOrder(-100)]
    public sealed class HighScoreSystem : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;

        [Header("Listens to")]
        [SerializeField] IntEventChannel scoreChanged;
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] VoidEventChannel mainMenuRequested;

        [Header("Raises")]
        [SerializeField] IntEventChannel highScoreChanged;

        IHighScoreStore store;
        int best;
        bool unsaved;

        public int HighScore => best;

        void Awake()
        {
            string file = rules != null ? rules.HighScoreFile : "highscore.json";
            store = new JsonHighScoreStore(Path.Combine(Application.persistentDataPath, file));
            best = store.Load();
        }

        void OnEnable()
        {
            if (scoreChanged != null) scoreChanged.Raised += OnScoreChanged;
            if (gameStateChanged != null) gameStateChanged.Raised += OnGameStateChanged;
            if (mainMenuRequested != null) mainMenuRequested.Raised += Persist;
        }

        void OnDisable()
        {
            if (scoreChanged != null) scoreChanged.Raised -= OnScoreChanged;
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (mainMenuRequested != null) mainMenuRequested.Raised -= Persist;

            // Covers quitting or stopping Play mid-game.
            Persist();
        }

        // Every listener has subscribed in its OnEnable by now, so the HUD and menu hear it.
        void Start() => Announce();

        void OnScoreChanged(int score)
        {
            if (score <= best)
                return;

            best = score;
            unsaved = true;
            Announce();
        }

        void OnGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
                Persist();
        }

        void Persist()
        {
            if (!unsaved)
                return;

            store.Save(best);
            unsaved = false;
        }

        void Announce()
        {
            if (highScoreChanged != null)
                highScoreChanged.Raise(best);
        }
    }
}

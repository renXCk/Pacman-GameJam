using Game.Core;
using Game.Events;
using UnityEngine;

namespace Game.Ghosts
{
    // Runs the ghosts of one maze. It keeps the global Scatter/Chase/Frightened clock, starts,
    // freezes and resets the ghosts as the game state changes, and decides what a touch means:
    // a catch, or a banish worth combo points. The rest of the game only hears about it
    // through event channels, and the director only learns about the game through them.
    public sealed class GhostDirector : MonoBehaviour
    {
        public enum ModeOverride
        {
            Auto,
            Scatter,
            Chase,
            Frightened
        }

        [Header("Data")]
        [Tooltip("Used until LevelStarted provides the level's own config.")]
        [SerializeField] LevelConfig level;
        [SerializeField] GhostAIConfig ai;
        [SerializeField] GhostRuntimeSet ghosts;
        [SerializeField] GhostMazeLayout layout;
        [SerializeField] TransformAnchor player;

        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] VoidEventChannel flareCollected;

        [Header("Raises")]
        [SerializeField] GhostModeEventChannel modeChanged;
        [SerializeField] VoidEventChannel playerCaught;
        [SerializeField] GhostBanishedEventChannel ghostBanished;

        [Header("Debug")]
        [Tooltip("Force every ghost into one mode while testing. Auto runs the normal timer.")]
        [SerializeField] ModeOverride forceMode = ModeOverride.Auto;

        readonly ScatterChaseTimer timer = new();
        ModeOverride appliedOverride = ModeOverride.Auto;
        bool running;
        bool needsReset = true;

        public LevelConfig Level => level;
        public GhostAIConfig AI => ai;
        public GhostMazeLayout Layout => layout;
        public bool IsRunning => running;
        public float RoundTime { get; private set; }

        // Ghosts banished with the current flare so far.
        public int Combo { get; private set; }

        // Goes up by one per flare, so a ghost revived during a flare is not scared again by the same one.
        public int FrightenedCycle { get; private set; }

        public GhostMode Mode => CurrentMode.Mode;
        public float ModeSecondsLeft => forceMode == ModeOverride.Auto ? timer.ModeSecondsLeft : 0f;
        public bool IsFrightened => Mode == GhostMode.Frightened;
        public bool FrightenedEnding => forceMode == ModeOverride.Auto && timer.IsFrightened &&
                                        timer.FrightenedLeft <= level.FrightenedWarningSeconds;

        public GhostMode BaseMode => forceMode switch
        {
            ModeOverride.Scatter => GhostMode.Scatter,
            ModeOverride.Chase => GhostMode.Chase,
            _ => timer.BaseMode
        };

        public bool HasPlayer => player != null && player.IsSet;
        public Vector3 PlayerPosition => HasPlayer ? player.Value.position : Vector3.zero;
        public Vector3 PlayerFacing => HasPlayer ? player.Value.forward : Vector3.forward;

        GhostModeChange CurrentMode => forceMode switch
        {
            ModeOverride.Scatter => new GhostModeChange(GhostMode.Scatter, 0f),
            ModeOverride.Chase => new GhostModeChange(GhostMode.Chase, 0f),
            ModeOverride.Frightened => new GhostModeChange(GhostMode.Frightened, 0f),
            _ => timer.Current
        };

        void OnEnable()
        {
            if (!IsConfigured())
            {
                enabled = false;
                return;
            }

            timer.Reset(level.ScatterChaseSeconds);

            if (gameStateChanged != null)
                gameStateChanged.Raised += OnGameStateChanged;
            if (levelStarted != null)
                levelStarted.Raised += OnLevelStarted;
            if (flareCollected != null)
                flareCollected.Raised += OnFlareCollected;

            ghosts.Added += Bind;
            for (int i = 0; i < ghosts.Count; i++)
                Bind(ghosts.Items[i]);
        }

        void OnDisable()
        {
            if (gameStateChanged != null)
                gameStateChanged.Raised -= OnGameStateChanged;
            if (levelStarted != null)
                levelStarted.Raised -= OnLevelStarted;
            if (flareCollected != null)
                flareCollected.Raised -= OnFlareCollected;
            if (ghosts != null)
                ghosts.Added -= Bind;
        }

        void Update()
        {
            if (forceMode != appliedOverride)
                ApplyOverride();

            if (!running)
                return;

            RoundTime += Time.deltaTime;

            if (forceMode == ModeOverride.Auto && timer.Tick(Time.deltaTime))
                BroadcastMode();
        }

        public Ghost FindGhost(GhostConfig config) => ghosts.Find(config);

        // A ghost's trigger touched the player.
        internal void ReportContact(Ghost ghost)
        {
            if (!running)
                return;

            if (ghost.State == GhostState.Frightened)
                Banish(ghost);
            else if (ghost.IsDangerous)
                CatchPlayer();
        }

        // Whether a ghost coming out of the house should be frightened.
        internal bool ShouldScare(int banishedInCycle)
        {
            if (forceMode == ModeOverride.Frightened)
                return true;

            return forceMode == ModeOverride.Auto && timer.IsFrightened && banishedInCycle != FrightenedCycle;
        }

        // Starts a round without a GameManager, for testing a maze on its own.
        [ContextMenu("Debug/Start Round Now")]
        void DebugStartRound()
        {
            PrepareRound();
            StartRound();
        }

        void OnGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    PrepareRound();
                    break;

                case GameState.Playing:
                    StartRound();
                    break;

                // PlayerCaught, LevelComplete, GameOver, Menu, Boot. A future Paused state should
                // only freeze, without asking for a reset, so Playing can resume where it was.
                default:
                    StopRound();
                    break;
            }
        }

        void OnLevelStarted(LevelConfig config)
        {
            if (config == null)
                return;

            level = config;
            for (int i = 0; i < ghosts.Count; i++)
                ghosts.Items[i].RefreshSpeed();
        }

        void OnFlareCollected()
        {
            if (!running || forceMode != ModeOverride.Auto || level.FrightenedSeconds <= 0f)
                return;

            Combo = 0;
            FrightenedCycle++;
            timer.StartFrightened(level.FrightenedSeconds);
            BroadcastMode();
        }

        void Bind(Ghost ghost)
        {
            if (ghost.ModeChannel != modeChanged)
                Debug.LogWarning($"{ghost.name} listens to a different ModeChanged channel than {name} raises.", ghost);

            ghost.Bind(this);

            // A ghost that turns up mid-round joins at its start position.
            if (needsReset)
                return;

            ghost.ResetToHome();
            if (running)
                ghost.Resume();
        }

        // Ready: everyone home, frozen, mode clock back to the first Scatter.
        void PrepareRound()
        {
            running = false;
            needsReset = false;
            RoundTime = 0f;
            Combo = 0;
            timer.Reset(level.ScatterChaseSeconds);

            for (int i = 0; i < ghosts.Count; i++)
                ghosts.Items[i].ResetToHome();
        }

        void StartRound()
        {
            if (running)
                return;

            if (needsReset)
                PrepareRound();

            running = true;
            for (int i = 0; i < ghosts.Count; i++)
                ghosts.Items[i].Resume();

            BroadcastMode();
        }

        void StopRound()
        {
            running = false;
            needsReset = true;

            for (int i = 0; i < ghosts.Count; i++)
                ghosts.Items[i].Freeze();
        }

        void Banish(Ghost ghost)
        {
            Combo++;
            int points = ai.BanishPoints(Combo);
            ghost.Banish();

            if (ghostBanished != null)
                ghostBanished.Raise(new GhostBanishedInfo(ghost.Config, Combo, points, ghost.transform.position));
        }

        void CatchPlayer()
        {
            // Freeze at once, so a second ghost touching in the same frame can't catch again.
            StopRound();

            if (playerCaught != null)
                playerCaught.Raise();
        }

        void ApplyOverride()
        {
            appliedOverride = forceMode;

            if (forceMode == ModeOverride.Frightened)
            {
                Combo = 0;
                FrightenedCycle++;
            }

            if (running)
                BroadcastMode();
        }

        void BroadcastMode() => modeChanged.Raise(CurrentMode);

        bool IsConfigured()
        {
            string missing = "";
            if (level == null) missing += " LevelConfig";
            if (ai == null) missing += " GhostAIConfig";
            if (ghosts == null) missing += " GhostRuntimeSet";
            if (modeChanged == null) missing += " ModeChanged channel";
            if (layout == null || !layout.IsComplete) missing += " GhostMazeLayout";

            if (missing.Length == 0)
                return true;

            Debug.LogError($"{name} is missing:{missing}. Ghosts will not run.", this);
            return false;
        }
    }
}

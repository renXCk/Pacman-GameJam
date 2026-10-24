using System.Collections;
using System.Text;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Sandbox
{
    // Stand-in for Dev C's GameManager so the ghosts can be played on their own. It raises the
    // same channels the real GameManager will (LevelStarted, GameStateChanged) and reacts to the
    // ghosts' PlayerCaught and GhostBanished. Remove it from scenes once GameManager exists.
    public sealed class StandaloneGameFlow : MonoBehaviour
    {
        [SerializeField] LevelConfig[] levels;
        [SerializeField] SandboxPlayer player;
        [SerializeField] GhostRuntimeSet ghosts;
        [SerializeField] Camera overviewCamera;

        [Header("Raises")]
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] GameStateEventChannel gameStateChanged;
        [Tooltip("Raised by the F key, the same as picking up a flare.")]
        [SerializeField] VoidEventChannel flareCollected;

        [Header("Listens to")]
        [SerializeField] VoidEventChannel playerCaught;
        [SerializeField] GhostBanishedEventChannel ghostBanished;
        [SerializeField] GhostModeEventChannel modeChanged;

        [Header("Flow")]
        [SerializeField] int startingLives = 3;
        [SerializeField] float readySeconds = 1.5f;
        [SerializeField] float caughtSeconds = 2f;
        [SerializeField] float overviewAmbient = 0.35f;
        [Tooltip("Off lets tests drive the game state themselves.")]
        [SerializeField] bool runOnStart = true;

        GameState state = GameState.Boot;
        int levelIndex;
        int lives;
        int score;
        GhostModeChange mode;
        float modeRaisedAt;
        string lastEvent = "";
        Color normalAmbient;
        bool overview;
        GUIStyle hudStyle;
        readonly StringBuilder hud = new();

        public GameState State => state;
        public int Lives => lives;
        public int Score => score;

        void OnEnable()
        {
            playerCaught.Raised += OnPlayerCaught;
            ghostBanished.Raised += OnGhostBanished;
            modeChanged.Raised += OnModeChanged;
        }

        void OnDisable()
        {
            playerCaught.Raised -= OnPlayerCaught;
            ghostBanished.Raised -= OnGhostBanished;
            modeChanged.Raised -= OnModeChanged;
        }

        void Start()
        {
            normalAmbient = RenderSettings.ambientLight;
            if (runOnStart)
                BeginGame();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.fKey.wasPressedThisFrame && state == GameState.Playing)
                flareCollected.Raise();

            if (keyboard.nKey.wasPressedThisFrame)
            {
                StopAllCoroutines();
                StartCoroutine(NextLevel());
            }

            if (keyboard.rKey.wasPressedThisFrame)
                BeginGame();

            if (keyboard.tabKey.wasPressedThisFrame)
                SetOverview(!overview);
        }

        public void BeginGame()
        {
            StopAllCoroutines();
            lives = startingLives;
            score = 0;
            lastEvent = "";
            StartCoroutine(BeginLevel(0));
        }

        public void SetOverview(bool on)
        {
            overview = on;
            if (overviewCamera != null)
                overviewCamera.enabled = on;
            RenderSettings.ambientLight = on ? Color.white * overviewAmbient : normalAmbient;
        }

        IEnumerator BeginLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
            levelStarted.Raise(levels[levelIndex]);
            yield return ReadyThenPlay();
        }

        IEnumerator ReadyThenPlay()
        {
            player.Respawn();
            SetState(GameState.Ready);
            yield return new WaitForSeconds(readySeconds);
            SetState(GameState.Playing);
        }

        IEnumerator NextLevel()
        {
            SetState(GameState.LevelComplete);
            lastEvent = "Level complete";
            yield return new WaitForSeconds(1f);
            yield return BeginLevel((levelIndex + 1) % levels.Length);
        }

        IEnumerator Caught()
        {
            SetState(GameState.PlayerCaught);
            lives--;
            lastEvent = "Caught!";
            player.InputEnabled = false;
            yield return new WaitForSeconds(caughtSeconds);
            player.InputEnabled = true;

            if (lives > 0)
            {
                yield return ReadyThenPlay();
                yield break;
            }

            SetState(GameState.GameOver);
            lastEvent = $"Game over with {score} points";
            yield return new WaitForSeconds(caughtSeconds);
            BeginGame();
        }

        void OnPlayerCaught()
        {
            if (state == GameState.Playing)
                StartCoroutine(Caught());
        }

        void OnGhostBanished(GhostBanishedInfo info)
        {
            score += info.Points;
            lastEvent = $"Banished {info}";
        }

        void OnModeChanged(GhostModeChange change)
        {
            mode = change;
            modeRaisedAt = Time.time;
        }

        void SetState(GameState next)
        {
            state = next;
            gameStateChanged.Raise(next);
        }

        void OnGUI()
        {
            hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };

            hud.Clear();
            hud.AppendLine("<b>GHOST SANDBOX</b>  (stand-in GameManager, test only)");
            hud.AppendLine($"Level {levelIndex + 1}   {state}   Lives {lives}   Score {score}");

            float left = mode.Seconds > 0f ? Mathf.Max(0f, mode.Seconds - (Time.time - modeRaisedAt)) : 0f;
            hud.AppendLine(mode.Seconds > 0f ? $"Mode {mode.Mode}, {left:0.0}s left" : $"Mode {mode.Mode}");

            for (int i = 0; i < ghosts.Count; i++)
            {
                Ghost ghost = ghosts.Items[i];
                string lit = ghost.IsLit ? "  (lit, slowed)" : "";
                hud.AppendLine($"  {ghost.Config.DisplayName,-7} {ghost.State}{lit}");
            }

            if (lastEvent.Length > 0)
                hud.AppendLine(lastEvent);

            hud.Append("<size=12>Click: lock mouse, then flashlight   WASD move   F flare   N next level   R restart   Tab map</size>");

            var area = new Rect(12f, 10f, 560f, 260f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(area.x - 6f, area.y - 4f, area.width, 24f * (5 + ghosts.Count)), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(area, hud.ToString(), hudStyle);
        }
    }
}

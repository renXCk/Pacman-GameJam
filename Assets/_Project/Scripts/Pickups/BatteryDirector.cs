using System;
using Game.Core;
using Game.Events;
using Game.Maze;
using UnityEngine;

namespace Game.Pickups
{
    // Task A7: the battery spawn rules. Batteries wait on the map's 'b' spots (under the ghost
    // house) and are hidden until the player has cleared a share of the level (BatteryRules). A
    // battery that appears stays for a fixed time, counted only while the game is Playing, then
    // goes away. Taking it, or letting it time out, frees its spot for the next one.
    //
    // A catch does not touch the batteries: one that is out stays out, and its clock is paused
    // until play resumes. A new level hides them all and starts counting again. Sits on the same
    // GameObject as MazeBuilder and MazePickups.
    public sealed class BatteryDirector : MonoBehaviour, IMazeListener
    {
        [SerializeField] MazePickups pickups;
        [SerializeField] LevelProgress progress;

        [Header("Rules")]
        [Tooltip("When a battery appears, as a share of the level's cells and flares taken (0 to 1). One entry per battery.")]
        [SerializeField] float[] spawnAtFractions = { 0.25f, 0.65f };

        [Tooltip("Seconds a battery waits before it goes away if nobody takes it. Only counts while playing.")]
        [Min(1f)] [SerializeField] float lifetimeSeconds = 20f;

        [Header("Listens to")]
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] GameStateEventChannel gameStateChanged;

        BatteryRules rules;
        float[] timeLeft = new float[0];
        int nextSpot;
        GameState state = GameState.Boot;

        // A battery appeared / timed out. Audio or the HUD can listen.
        public event Action<Pickup> Spawned;
        public event Action<Pickup> Expired;

        public float LifetimeSeconds => lifetimeSeconds;

        public int SpawnedThisLevel => rules != null ? rules.Spawned : 0;

        // Seconds left on the battery at a spot, or 0 if that spot is empty.
        public float TimeLeft(int spot) =>
            spot >= 0 && spot < timeLeft.Length && pickups != null && pickups.Batteries[spot].IsAvailable ? timeLeft[spot] : 0f;

        void Awake() => EnsureRules();

        void EnsureRules()
        {
            if (rules == null)
                rules = new BatteryRules(spawnAtFractions);
        }

        void OnEnable()
        {
            if (levelStarted != null)
                levelStarted.Raised += OnLevelStarted;
            if (gameStateChanged != null)
                gameStateChanged.Raised += OnGameStateChanged;
            if (progress != null)
                progress.Changed += OnProgressChanged;
        }

        void OnDisable()
        {
            if (levelStarted != null)
                levelStarted.Raised -= OnLevelStarted;
            if (gameStateChanged != null)
                gameStateChanged.Raised -= OnGameStateChanged;
            if (progress != null)
                progress.Changed -= OnProgressChanged;
        }

        // The maze was (re)built. MazePickups may or may not have run yet, so this only claims the
        // batteries: MazePickups hides them itself if it builds after this.
        public void OnMazeBuilt(MazeBuilder builder)
        {
            EnsureRules();
            rules = new BatteryRules(spawnAtFractions);

            if (pickups == null)
                pickups = GetComponent<MazePickups>();

            if (pickups == null)
            {
                Debug.LogError("BatteryDirector needs a MazePickups on the same object. Run Game > Pickups > Add Battery Rules And Warps.", this);
                return;
            }

            pickups.SetBatteriesManaged(true);
            ResetLevel();
        }

        void Update()
        {
            if (state == GameState.Playing)
                Tick(Time.deltaTime);
        }

        // Counts the batteries' clocks down. Update calls it with the frame time while playing.
        public void Tick(float deltaTime)
        {
            if (pickups == null)
                return;

            EnsureTimers();
            for (int i = 0; i < pickups.Batteries.Count; i++)
            {
                Pickup battery = pickups.Batteries[i];
                if (!battery.IsAvailable)
                    continue;

                timeLeft[i] -= deltaTime;
                if (timeLeft[i] > 0f)
                    continue;

                timeLeft[i] = 0f;
                battery.Hide();
                Expired?.Invoke(battery);
            }
        }

        void OnLevelStarted(LevelConfig level) => ResetLevel();

        void OnGameStateChanged(GameState next) => state = next;

        void OnProgressChanged(LevelProgress changed)
        {
            if (rules == null || pickups == null)
                return;

            int due = rules.TakeDue(changed.Collected, changed.Total);
            for (int i = 0; i < due; i++)
                SpawnBattery();
        }

        // A new level (or a fresh maze): no batteries out and every threshold counts again.
        void ResetLevel()
        {
            EnsureRules();
            rules.Begin();
            nextSpot = 0;

            if (pickups == null)
                return;

            EnsureTimers();
            for (int i = 0; i < pickups.Batteries.Count; i++)
            {
                pickups.Batteries[i].Hide();
                timeLeft[i] = 0f;
            }
        }

        // Puts a battery on the next free spot, going round the spots in map order. With no free
        // spot the battery is skipped: there is never more than one battery per spot.
        void SpawnBattery()
        {
            int count = pickups.Batteries.Count;
            if (count == 0)
                return;

            EnsureTimers();
            for (int step = 0; step < count; step++)
            {
                int spot = (nextSpot + step) % count;
                Pickup battery = pickups.Batteries[spot];
                if (battery.IsAvailable)
                    continue;

                battery.Restore();
                timeLeft[spot] = lifetimeSeconds;
                nextSpot = (spot + 1) % count;
                Spawned?.Invoke(battery);
                return;
            }
        }

        void EnsureTimers()
        {
            int count = pickups != null ? pickups.Batteries.Count : 0;
            if (timeLeft.Length != count)
                timeLeft = new float[count];
        }
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Events;
using Game.Maze;
using Game.Pickups;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A7: batteries start hidden, appear as the level is cleared, time out, and reset with the level.
    // Built far from the origin; counts only its own objects (the open Game scene has its own maze).
    public sealed class BatteryDirectorTests
    {
        // 16 cells, 1 flare, 1 battery spot. Total 17.
        const string OneSpot =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P..o#\n" +
            "#########\n";

        // 16 cells, no flare, 2 battery spots. Total 16.
        const string TwoSpots =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P.b.#\n" +
            "#########\n";

        static readonly Vector3 TestOrigin = new Vector3(5000f, 0f, 5000f);

        sealed class Rig
        {
            public MazeBuilder Builder;
            public MazePickups Pickups;
            public BatteryDirector Director;
            public LevelProgress Progress;
            public LevelConfigEventChannel LevelStarted;
            public GameStateEventChannel State;
            public Collider Player;
            public PlayerFlashlight Flashlight;
        }

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        PickupConfig MakeConfig(PickupKind kind, bool counts)
        {
            var config = Make<PickupConfig>();
            SetField(config, "kind", kind);
            SetField(config, "displayName", kind.ToString());
            SetField(config, "countsTowardLevel", counts);
            return config;
        }

        Rig MakeRig(string map, float[] fractions, float lifetime)
        {
            var rig = new Rig
            {
                Progress = Make<LevelProgress>(),
                LevelStarted = Make<LevelConfigEventChannel>(),
                State = Make<GameStateEventChannel>()
            };

            var mazeConfig = Make<MazeConfig>();
            SetField(mazeConfig, "mapFile", new TextAsset(map));

            var go = new GameObject("Test Battery Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin;

            rig.Builder = go.AddComponent<MazeBuilder>();
            rig.Pickups = go.AddComponent<MazePickups>();
            rig.Director = go.AddComponent<BatteryDirector>();

            SetField(rig.Builder, "config", mazeConfig);
            SetField(rig.Pickups, "cellConfig", MakeConfig(PickupKind.Cell, true));
            SetField(rig.Pickups, "flareConfig", MakeConfig(PickupKind.Flare, true));
            SetField(rig.Pickups, "batteryConfig", MakeConfig(PickupKind.Battery, false));
            SetField(rig.Pickups, "progress", rig.Progress);
            SetField(rig.Pickups, "levelStarted", rig.LevelStarted);
            SetField(rig.Pickups, "pickupCollected", Make<PickupEventChannel>());
            SetField(rig.Pickups, "flareCollected", Make<VoidEventChannel>());

            SetField(rig.Director, "pickups", rig.Pickups);
            SetField(rig.Director, "progress", rig.Progress);
            SetField(rig.Director, "spawnAtFractions", fractions);
            SetField(rig.Director, "lifetimeSeconds", lifetime);
            SetField(rig.Director, "levelStarted", rig.LevelStarted);
            SetField(rig.Director, "gameStateChanged", rig.State);

            go.SetActive(true);

            // A stand-in player, parked high above the maze, tagged Player, with the flashlight.
            var playerConfig = Make<PlayerConfig>();
            var player = new GameObject("Test Player");
            created.Add(player);
            player.SetActive(false);
            player.tag = Pickup.PlayerTag;
            player.transform.position = TestOrigin + Vector3.up * 20f;
            rig.Player = player.AddComponent<BoxCollider>();
            rig.Flashlight = player.AddComponent<PlayerFlashlight>();
            SetField(rig.Flashlight, "config", playerConfig);
            player.SetActive(true);

            // The game raises this at the start of every level; it restarts the count.
            rig.LevelStarted.Raise(null);
            return rig;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        // Takes cells (not flares) until the level count reaches the number.
        static void TakeUntil(Rig rig, int collected)
        {
            for (int i = 0; i < rig.Pickups.Cells.Count && rig.Progress.Collected < collected; i++)
                rig.Pickups.Cells[i].Touch(rig.Player);

            Assert.That(rig.Progress.Collected, Is.EqualTo(collected));
        }

        static int BatteriesOut(Rig rig)
        {
            int count = 0;
            for (int i = 0; i < rig.Pickups.Batteries.Count; i++)
            {
                if (rig.Pickups.Batteries[i].IsAvailable)
                    count++;
            }

            return count;
        }

        [Test]
        public void BatteriesStartHidden_WhileEverythingElseIsOut()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f, 0.5f }, 20f);

            Assert.That(rig.Pickups.Batteries.Count, Is.EqualTo(1));
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
            foreach (Pickup cell in rig.Pickups.Cells)
                Assert.That(cell.IsAvailable, Is.True);
            Assert.That(rig.Progress.Total, Is.EqualTo(17));
        }

        [Test]
        public void TheFirstBatteryAppearsWhenTheFirstThresholdIsCrossed()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f, 0.5f }, 20f);

            int spawned = 0;
            rig.Director.Spawned += battery => spawned++;

            TakeUntil(rig, 4); // a quarter of 17 is 4.25, so 5 are needed
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));

            TakeUntil(rig, 5);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1));
            Assert.That(spawned, Is.EqualTo(1));
            Assert.That(rig.Director.TimeLeft(0), Is.EqualTo(20f).Within(0.001f));
            Assert.That(rig.Director.SpawnedThisLevel, Is.EqualTo(1));
        }

        [Test]
        public void TakingABattery_FreesTheSpotForTheNextThreshold()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f, 0.5f }, 20f);
            rig.Flashlight.Recharge(-80f);

            TakeUntil(rig, 5);
            Pickup battery = rig.Pickups.Batteries[0];
            Assert.That(battery.Touch(rig.Player), Is.True);
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.7f).Within(0.001f), "the battery recharged the flashlight");
            Assert.That(rig.Progress.Collected, Is.EqualTo(5), "and did not count toward the level");

            TakeUntil(rig, 8);
            Assert.That(BatteriesOut(rig), Is.EqualTo(0), "half of 17 is 8.5, so 9 are needed");

            TakeUntil(rig, 9);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1), "the second threshold brings it back to the same spot");
            Assert.That(rig.Director.SpawnedThisLevel, Is.EqualTo(2));
        }

        [Test]
        public void ABatteryThatIsNotTaken_TimesOut()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f }, 10f);

            int expired = 0;
            rig.Director.Expired += battery => expired++;

            TakeUntil(rig, 5);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1));

            rig.Director.Tick(9f);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1));
            Assert.That(rig.Director.TimeLeft(0), Is.EqualTo(1f).Within(0.001f));

            rig.Director.Tick(1.5f);
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
            Assert.That(expired, Is.EqualTo(1));
            Assert.That(rig.Director.TimeLeft(0), Is.EqualTo(0f));

            // Timing out does not give the threshold back: that battery is gone for this level.
            TakeUntil(rig, 9);
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
        }

        [Test]
        public void SeveralThresholdsAtOnce_UseDifferentSpots_AndNeverMoreThanTheMapHas()
        {
            Rig rig = MakeRig(TwoSpots, new[] { 0.25f, 0.25f, 0.25f }, 20f);
            Assert.That(rig.Pickups.Batteries.Count, Is.EqualTo(2));

            TakeUntil(rig, 4); // a quarter of 16 is 4

            Assert.That(rig.Pickups.Batteries[0].IsAvailable, Is.True);
            Assert.That(rig.Pickups.Batteries[1].IsAvailable, Is.True);
            Assert.That(BatteriesOut(rig), Is.EqualTo(2), "the third battery found no free spot and was skipped");
            Assert.That(rig.Director.SpawnedThisLevel, Is.EqualTo(3));
        }

        [Test]
        public void ANewLevel_HidesTheBatteries_AndTheThresholdsCountAgain()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f }, 20f);

            TakeUntil(rig, 5);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1));

            rig.LevelStarted.Raise(null);

            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
            Assert.That(rig.Progress.Collected, Is.EqualTo(0));
            Assert.That(rig.Director.SpawnedThisLevel, Is.EqualTo(0));
            foreach (Pickup cell in rig.Pickups.Cells)
                Assert.That(cell.IsAvailable, Is.True, "the level's cells are back");

            TakeUntil(rig, 5);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1), "the first threshold pays out again");
        }

        [Test]
        public void ARebuiltMaze_StillHidesTheBatteries()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f }, 20f);
            rig.Builder.Build();
            rig.LevelStarted.Raise(null);

            Assert.That(rig.Pickups.Batteries.Count, Is.EqualTo(1));
            Assert.That(BatteriesOut(rig), Is.EqualTo(0));
        }

        // The clock runs only while the game is Playing, so a catch (and the Ready that follows)
        // does not use up a battery's time.
        [UnityTest]
        public IEnumerator TheBatteryClockOnlyRunsWhilePlaying()
        {
            Rig rig = MakeRig(OneSpot, new[] { 0.25f }, 0.4f);

            TakeUntil(rig, 5);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1));

            rig.State.Raise(GameState.PlayerCaught);
            yield return new WaitForSeconds(0.8f);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1), "a catch pauses the clock, and the battery stays out");

            rig.State.Raise(GameState.Ready);
            yield return new WaitForSeconds(0.8f);
            Assert.That(BatteriesOut(rig), Is.EqualTo(1), "so does Ready");

            rig.State.Raise(GameState.Playing);
            float giveUp = Time.realtimeSinceStartup + 3f;
            while (BatteriesOut(rig) > 0 && Time.realtimeSinceStartup < giveUp)
                yield return null;

            Assert.That(BatteriesOut(rig), Is.EqualTo(0), "the clock runs again once play resumes");
        }
    }
}
#endif

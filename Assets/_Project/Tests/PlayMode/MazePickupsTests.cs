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
    // Task A5: the maze fills with cells, flares and batteries that do the right thing when taken.
    // Everything is built far from the origin and counts only its own objects, because the open
    // Game scene has a maze, pickups and a player of its own.
    public sealed class MazePickupsTests
    {
        // 9 columns by 7 rows: 16 cells, 1 flare, 1 battery. With 3 m tiles: x = col * 3, z = (6 - row) * 3.
        const string Small =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P..o#\n" +
            "#########\n";

        const int SmallCells = 16;
        const int SmallFlares = 1;
        const int SmallBatteries = 1;

        static readonly Vector3 TestOrigin = new Vector3(4000f, 0f, 4000f);

        sealed class Rig
        {
            public MazeBuilder Builder;
            public MazePickups Pickups;
            public LevelProgress Progress;
            public PickupEventChannel PickupEvent;
            public VoidEventChannel FlareEvent;
            public LevelConfigEventChannel LevelStarted;
            public PickupConfig Cell;
            public PickupConfig Flare;
            public PickupConfig Battery;
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

        Rig MakeRig()
        {
            var rig = new Rig
            {
                Progress = Make<LevelProgress>(),
                PickupEvent = Make<PickupEventChannel>(),
                FlareEvent = Make<VoidEventChannel>(),
                LevelStarted = Make<LevelConfigEventChannel>(),
                Cell = MakeConfig(PickupKind.Cell, true),
                Flare = MakeConfig(PickupKind.Flare, true),
                Battery = MakeConfig(PickupKind.Battery, false)
            };

            var mazeConfig = Make<MazeConfig>();
            SetField(mazeConfig, "mapFile", new TextAsset(Small));

            var go = new GameObject("Test Pickup Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin;

            rig.Builder = go.AddComponent<MazeBuilder>();
            rig.Pickups = go.AddComponent<MazePickups>();

            SetField(rig.Builder, "config", mazeConfig);
            SetField(rig.Pickups, "cellConfig", rig.Cell);
            SetField(rig.Pickups, "flareConfig", rig.Flare);
            SetField(rig.Pickups, "batteryConfig", rig.Battery);
            SetField(rig.Pickups, "progress", rig.Progress);
            SetField(rig.Pickups, "levelStarted", rig.LevelStarted);
            SetField(rig.Pickups, "pickupCollected", rig.PickupEvent);
            SetField(rig.Pickups, "flareCollected", rig.FlareEvent);

            go.SetActive(true);
            return rig;
        }

        // A stand-in for the player: tagged Player, with a collider and the flashlight.
        PlayerFlashlight MakePlayer(Vector3 position)
        {
            var playerConfig = Make<PlayerConfig>();

            var go = new GameObject("Test Player");
            created.Add(go);
            go.SetActive(false);
            go.tag = Pickup.PlayerTag;
            go.transform.position = position;
            go.AddComponent<BoxCollider>();

            var flashlight = go.AddComponent<PlayerFlashlight>();
            SetField(flashlight, "config", playerConfig);

            go.SetActive(true);
            return flashlight;
        }

        Collider MakeStranger()
        {
            var go = new GameObject("Not The Player");
            created.Add(go);
            return go.AddComponent<BoxCollider>();
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        static bool IsShown(Pickup pickup)
        {
            var trigger = pickup.GetComponent<Collider>();
            var renderer = pickup.GetComponentInChildren<Renderer>(true);
            return trigger.enabled && renderer.enabled;
        }

        [Test]
        public void BuildsOnePickupPerMapTile()
        {
            Rig rig = MakeRig();

            Assert.That(rig.Pickups.Cells.Count, Is.EqualTo(SmallCells));
            Assert.That(rig.Pickups.Flares.Count, Is.EqualTo(SmallFlares));
            Assert.That(rig.Pickups.Batteries.Count, Is.EqualTo(SmallBatteries));
            Assert.That(rig.Pickups.CompletionTotal, Is.EqualTo(rig.Builder.Grid.CompletionTotal));
            Assert.That(rig.Builder.GetComponentsInChildren<Pickup>(false).Length, Is.EqualTo(SmallCells + SmallFlares + SmallBatteries));

            foreach (Pickup cell in rig.Pickups.Cells)
                Assert.That(cell.Kind, Is.EqualTo(PickupKind.Cell));
            foreach (Pickup flare in rig.Pickups.Flares)
                Assert.That(flare.Kind, Is.EqualTo(PickupKind.Flare));
            foreach (Pickup battery in rig.Pickups.Batteries)
                Assert.That(battery.Kind, Is.EqualTo(PickupKind.Battery));
        }

        [Test]
        public void PickupsSitOnTheirTilesAtTheArtGuideHeights()
        {
            Rig rig = MakeRig();
            MazeGrid grid = rig.Builder.Grid;

            TileCoord flareTile = grid.Find(MazeTile.Flare)[0];
            Vector3 flareExpected = rig.Builder.TileCenter(flareTile) + Vector3.up * MazePickups.FlareHeight;
            Assert.That(Vector3.Distance(rig.Pickups.Flares[0].transform.position, flareExpected), Is.LessThan(0.01f));

            TileCoord batteryTile = grid.Find(MazeTile.Battery)[0];
            Vector3 batteryExpected = rig.Builder.TileCenter(batteryTile) + Vector3.up * MazePickups.BatteryHeight;
            Assert.That(Vector3.Distance(rig.Pickups.Batteries[0].transform.position, batteryExpected), Is.LessThan(0.01f));

            // Cells are in reading order, so the first one is the first '.' on the map.
            TileCoord cellTile = grid.Find(MazeTile.Cell)[0];
            Vector3 cellExpected = rig.Builder.TileCenter(cellTile) + Vector3.up * MazePickups.CellHeight;
            Assert.That(Vector3.Distance(rig.Pickups.Cells[0].transform.position, cellExpected), Is.LessThan(0.01f));
        }

        [Test]
        public void PickupsAreTriggerSpheresWithNoLights()
        {
            Rig rig = MakeRig();
            Pickup[] all = rig.Builder.GetComponentsInChildren<Pickup>(false);
            Assert.That(all.Length, Is.GreaterThan(0));

            foreach (Pickup pickup in all)
            {
                var sphere = pickup.GetComponent<SphereCollider>();
                Assert.That(sphere, Is.Not.Null, pickup.name);
                Assert.That(sphere.isTrigger, Is.True, pickup.name);
                Assert.That(pickup.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(0), pickup.name + " must not have a light");

                foreach (Collider collider in pickup.GetComponentsInChildren<Collider>(true))
                    Assert.That(collider.isTrigger, Is.True, pickup.name + " must not have a solid collider");
            }

            Assert.That(rig.Pickups.Cells[0].GetComponent<SphereCollider>().radius, Is.EqualTo(MazePickups.CellRadius).Within(0.001f));
            Assert.That(rig.Pickups.Flares[0].GetComponent<SphereCollider>().radius, Is.EqualTo(MazePickups.FlareRadius).Within(0.001f));
            Assert.That(rig.Pickups.Batteries[0].GetComponent<SphereCollider>().radius, Is.EqualTo(MazePickups.BatteryRadius).Within(0.001f));
        }

        [Test]
        public void TakingACell_RaisesPickupCollected_AndCountsTowardTheLevel()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();
            Assert.That(rig.Progress.Total, Is.EqualTo(SmallCells + SmallFlares));

            var heard = new List<PickupConfig>();
            int flares = 0;
            rig.PickupEvent.Raised += config => heard.Add(config);
            rig.FlareEvent.Raised += () => flares++;

            Pickup cell = rig.Pickups.Cells[0];
            Assert.That(cell.Touch(MakePlayer(TestOrigin + Vector3.up * 20f).GetComponent<Collider>()), Is.True);

            Assert.That(heard.Count, Is.EqualTo(1));
            Assert.That(heard[0], Is.SameAs(rig.Cell));
            Assert.That(flares, Is.EqualTo(0));
            Assert.That(rig.Progress.Collected, Is.EqualTo(1));
            Assert.That(cell.IsCollected, Is.True);
            Assert.That(IsShown(cell), Is.False, "a taken cell disappears and stops being a trigger");
        }

        [Test]
        public void TakingAFlare_RaisesOnlyFlareCollected_AndCountsTowardTheLevel()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            int picked = 0;
            int flares = 0;
            rig.PickupEvent.Raised += config => picked++;
            rig.FlareEvent.Raised += () => flares++;

            Collider player = MakePlayer(TestOrigin + Vector3.up * 20f).GetComponent<Collider>();
            Assert.That(rig.Pickups.Flares[0].Touch(player), Is.True);

            Assert.That(flares, Is.EqualTo(1));
            Assert.That(picked, Is.EqualTo(0), "flares do not raise PickupCollected");
            Assert.That(rig.Progress.Collected, Is.EqualTo(1));
        }

        [Test]
        public void TakingABattery_RechargesTheFlashlight_AndNeverCounts()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            PlayerFlashlight flashlight = MakePlayer(TestOrigin + Vector3.up * 20f);
            flashlight.Recharge(-80f); // a drained battery: 20 of 100
            Assert.That(flashlight.Fraction, Is.EqualTo(0.2f).Within(0.001f));

            var heard = new List<PickupConfig>();
            rig.PickupEvent.Raised += config => heard.Add(config);

            Assert.That(rig.Pickups.Batteries[0].Touch(flashlight.GetComponent<Collider>()), Is.True);

            Assert.That(heard.Count, Is.EqualTo(1));
            Assert.That(heard[0], Is.SameAs(rig.Battery));
            Assert.That(flashlight.Fraction, Is.EqualTo(0.7f).Within(0.001f), "one battery adds PlayerConfig.RechargeAmount (50)");
            Assert.That(rig.Progress.Collected, Is.EqualTo(0), "batteries are a bonus, not part of the level");
        }

        [Test]
        public void OnlyThePlayerCanTakeAPickup()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            Pickup cell = rig.Pickups.Cells[0];
            Assert.That(cell.Touch(MakeStranger()), Is.False);
            Assert.That(cell.Touch(null), Is.False);
            Assert.That(cell.IsAvailable, Is.True);
            Assert.That(rig.Progress.Collected, Is.EqualTo(0));
        }

        [Test]
        public void ATakenPickupCannotBeTakenTwice()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            int heard = 0;
            rig.PickupEvent.Raised += config => heard++;

            Collider player = MakePlayer(TestOrigin + Vector3.up * 20f).GetComponent<Collider>();
            Pickup cell = rig.Pickups.Cells[0];
            Assert.That(cell.Touch(player), Is.True);
            Assert.That(cell.Touch(player), Is.False);

            Assert.That(heard, Is.EqualTo(1));
            Assert.That(rig.Progress.Collected, Is.EqualTo(1));
        }

        [Test]
        public void TakingEveryCellAndFlare_CompletesTheLevelExactlyOnce()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            int completed = 0;
            rig.Progress.Completed += () => completed++;

            Collider player = MakePlayer(TestOrigin + Vector3.up * 20f).GetComponent<Collider>();
            foreach (Pickup cell in rig.Pickups.Cells)
                cell.Touch(player);
            Assert.That(completed, Is.EqualTo(0), "the flare is still out there");

            foreach (Pickup flare in rig.Pickups.Flares)
                flare.Touch(player);

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(rig.Progress.IsComplete, Is.True);
            Assert.That(rig.Progress.Collected, Is.EqualTo(SmallCells + SmallFlares));
            Assert.That(rig.Pickups.CollectedCount, Is.EqualTo(SmallCells + SmallFlares));
        }

        [Test]
        public void ANewLevel_PutsEveryPickupBack_AndRestartsTheCount()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            Collider player = MakePlayer(TestOrigin + Vector3.up * 20f).GetComponent<Collider>();
            rig.Pickups.Cells[0].Touch(player);
            rig.Pickups.Cells[1].Touch(player);
            rig.Pickups.Flares[0].Touch(player);
            rig.Pickups.Batteries[0].Touch(player);
            Assert.That(rig.Progress.Collected, Is.EqualTo(3));

            rig.LevelStarted.Raise(null); // what GameManager does at the start of every level

            Assert.That(rig.Progress.Collected, Is.EqualTo(0));
            Assert.That(rig.Progress.Total, Is.EqualTo(SmallCells + SmallFlares));
            Assert.That(rig.Pickups.CollectedCount, Is.EqualTo(0));

            foreach (Pickup pickup in rig.Builder.GetComponentsInChildren<Pickup>(false))
            {
                Assert.That(pickup.IsAvailable, Is.True, pickup.name);
                Assert.That(IsShown(pickup), Is.True, pickup.name);
            }
        }

        [Test]
        public void RebuildingTheMaze_ReplacesThePickups_InsteadOfAddingMore()
        {
            Rig rig = MakeRig();
            rig.Builder.Build();

            Assert.That(rig.Pickups.Cells.Count, Is.EqualTo(SmallCells));
            Assert.That(rig.Builder.GetComponentsInChildren<Pickup>(false).Length, Is.EqualTo(SmallCells + SmallFlares + SmallBatteries),
                "the old pickups are inactive, so only the new ones are counted");
        }

        [Test]
        public void ManagedBatteries_StartHidden_AndStayHiddenOnANewLevel()
        {
            Rig rig = MakeRig();
            Pickup battery = rig.Pickups.Batteries[0];
            Assert.That(battery.IsAvailable, Is.True, "unmanaged batteries are always out");

            rig.Pickups.SetBatteriesManaged(true);
            Assert.That(battery.IsAvailable, Is.False);

            rig.Pickups.BeginLevel();
            Assert.That(battery.IsAvailable, Is.False, "a new level leaves the battery rule in charge");
            Assert.That(rig.Pickups.Cells[0].IsAvailable, Is.True);

            rig.Pickups.SetBatteriesManaged(false);
            rig.Pickups.RestoreAll();
            Assert.That(battery.IsAvailable, Is.True);
        }

        // The real physics path: a CharacterController walking into the trigger takes the cell.
        [UnityTest]
        public IEnumerator WalkingIntoACell_TakesIt()
        {
            Rig rig = MakeRig();
            rig.Pickups.BeginLevel();

            Pickup cell = rig.Pickups.Cells[0];
            Vector3 feet = cell.transform.position - Vector3.up * MazePickups.CellHeight;

            var go = new GameObject("Walking Test Player");
            created.Add(go);
            go.tag = Pickup.PlayerTag;
            go.transform.position = feet + new Vector3(1.5f, 0f, 0f);

            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);

            // Walk in from the side, then wobble in place, for up to a second of physics.
            float giveUp = Time.realtimeSinceStartup + 2f;
            int frame = 0;
            while (!cell.IsCollected && Time.realtimeSinceStartup < giveUp)
            {
                Vector3 toCell = feet - go.transform.position;
                toCell.y = 0f;
                Vector3 step = toCell.magnitude > 0.05f ? toCell.normalized * 0.1f : new Vector3((frame % 2 == 0 ? 1f : -1f) * 0.02f, 0f, 0f);
                body.Move(step);
                frame++;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(cell.IsCollected, Is.True, "the cell was taken by walking into it");
            Assert.That(rig.Progress.Collected, Is.EqualTo(1));
        }
    }
}
#endif

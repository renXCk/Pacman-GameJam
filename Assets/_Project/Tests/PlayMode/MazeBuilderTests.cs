#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A2: the builder makes a walkable maze from a map. These tests build their own tiny
    // maze (no scene, no assets besides the Level 1 file) and check it with physics queries.
    public sealed class MazeBuilderTests
    {
        const string Level1Path = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        // 9 columns by 7 rows. With 3 m tiles: x = col * 3, z = (6 - row) * 3.
        const string Small =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#...P..o#\n" +
            "#########\n";

        // Built far from the origin: other tests and scenes leave colliders near it.
        static readonly Vector3 TestOrigin = new Vector3(2000f, 0f, 2000f);

        readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

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

        MazeBuilder MakeBuilder(string map)
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(map));

            // Inactive while the fields are set, so Awake (which builds) sees the config.
            var go = new GameObject("Test Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin; // far from anything a previous test or scene left behind
            var builder = go.AddComponent<MazeBuilder>();
            SetField(builder, "config", config);
            go.SetActive(true);
            return builder;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        static bool Blocked(Vector3 center, float radius) => Physics.CheckSphere(center + Vector3.up * 1f, radius);

        [Test]
        public void Builds_OnAwake_FromTheConfig()
        {
            MazeBuilder builder = MakeBuilder(Small);

            Assert.That(builder.IsBuilt, Is.True);
            Assert.That(builder.Grid.Rows, Is.EqualTo(7));
            Assert.That(builder.Grid.Cols, Is.EqualTo(9));
            Assert.That(builder.Walls, Is.Not.Null);
            Assert.That(builder.HouseDoor, Is.Not.Null);
        }

        [Test]
        public void MakesOneCubePerWallRun()
        {
            MazeBuilder builder = MakeBuilder(Small);

            Assert.That(builder.Walls.childCount, Is.EqualTo(MazeWallRuns.Compute(builder.Grid).Count));
            Assert.That(builder.Walls.childCount, Is.EqualTo(17));
        }

        [Test]
        public void CorridorTilesAreOpenAndWallTilesAreSolid()
        {
            MazeBuilder builder = MakeBuilder(Small);

            // Walkable: the player start, a house tile, the tile above the door.
            Assert.That(Blocked(builder.TileCenter(builder.Grid.PlayerStart), 0.4f), Is.False, "player start");
            Assert.That(Blocked(builder.TileCenter(builder.Grid.GhostExit), 0.4f), Is.False, "ghost exit");
            Assert.That(Blocked(builder.TileCenter(3, 4), 0.4f), Is.False, "house tile");

            // Solid: a corner, and the wall block between the corridor and the house.
            Assert.That(Blocked(builder.TileCenter(0, 0), 0.2f), Is.True, "corner");
            Assert.That(Blocked(builder.TileCenter(2, 2), 0.2f), Is.True, "house wall");
            Assert.That(Blocked(builder.TileCenter(4, 4), 0.2f), Is.True, "wall under the house");
        }

        [Test]
        public void TheMapIsNotMirrored()
        {
            MazeBuilder builder = MakeBuilder(Small);

            // The player start is at row 5, col 4 -> 12 m right of and 3 m above the maze origin.
            Vector3 start = builder.TileCenter(builder.Grid.PlayerStart);
            Assert.That(start.x - TestOrigin.x, Is.EqualTo(12f).Within(0.001f));
            Assert.That(start.z - TestOrigin.z, Is.EqualTo(3f).Within(0.001f));

            // The bottom row of the map is wall at z = 0: a ray down onto it lands on the wall top.
            Vector3 aboveBottomWall = TestOrigin + new Vector3(12f, 10f, 0f);
            Assert.That(Physics.Raycast(aboveBottomWall, Vector3.down, out RaycastHit hit, 20f), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(3.3f).Within(0.01f), "wall height");

            // And the floor under the player start is at y = 0.
            Assert.That(Physics.Raycast(start + Vector3.up * 5f, Vector3.down, out RaycastHit floorHit, 20f), Is.True);
            Assert.That(floorHit.point.y, Is.EqualTo(0f).Within(0.01f), "floor height");
        }

        [Test]
        public void HouseDoor_IsOnTheIgnoreRaycastLayer_AndBlocksThePlayer()
        {
            MazeBuilder builder = MakeBuilder(Small);
            Transform door = builder.HouseDoor;

            Assert.That(door.gameObject.layer, Is.EqualTo(MazeBuilder.DoorLayer));
            Assert.That(MazeBuilder.DoorLayer, Is.EqualTo(2));

            Vector3 expected = builder.TileCenter(builder.Grid.HouseDoor);
            Assert.That(door.position.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(door.position.z, Is.EqualTo(expected.z).Within(0.001f));

            // It has a collider, so the player cannot walk into the house. (Ignore Raycast is left out
            // of Unity's default query mask, so ask for every layer.)
            Assert.That(door.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(Physics.CheckBox(door.position, new Vector3(0.2f, 0.2f, 0.05f), Quaternion.identity, ~0), Is.True);

            // The door spans the corridor gap sideways (house is below the exit on the map).
            Assert.That(door.localScale.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(door.localScale.z, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator Rebuilding_ReplacesTheOldMaze()
        {
            MazeBuilder builder = MakeBuilder(Small);
            Transform firstGeometry = builder.Geometry;

            builder.Build();
            yield return null;

            Assert.That(firstGeometry == null, Is.True, "the old maze is destroyed");
            Assert.That(builder.Geometry, Is.Not.Null);
            Assert.That(builder.Walls.childCount, Is.EqualTo(17));
            Assert.That(builder.transform.childCount, Is.EqualTo(1), "only one Maze Geometry object");
        }

        [Test]
        public void Level1File_BuildsWithNinetyTwoWallCubes()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(asset, Is.Not.Null, $"Missing {Level1Path}");

            MazeBuilder builder = MakeBuilder(asset.text);

            Assert.That(builder.IsBuilt, Is.True);
            Assert.That(builder.Walls.childCount, Is.EqualTo(92));
            Assert.That(Blocked(builder.TileCenter(builder.Grid.PlayerStart), 0.4f), Is.False, "player start");
        }

        [Test]
        public void ListenersRunBeforeBuiltIsRaised()
        {
            MazeBuilder builder = MakeBuilder(Small);
            var listener = builder.gameObject.AddComponent<RecordingListener>();
            int builtEvents = 0;
            builder.Built += _ => builtEvents++;

            builder.Build();

            Assert.That(listener.Calls, Is.EqualTo(1));
            Assert.That(listener.SawGrid, Is.True);
            Assert.That(builtEvents, Is.EqualTo(1));
        }

        public sealed class RecordingListener : MonoBehaviour, IMazeListener
        {
            public int Calls;
            public bool SawGrid;

            public void OnMazeBuilt(MazeBuilder builder)
            {
                Calls++;
                SawGrid = builder.Grid != null && builder.Walls != null;
            }
        }
    }
}
#endif

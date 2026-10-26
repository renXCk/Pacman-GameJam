#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Ghosts;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A3: the maze hands the ghosts their layout points and spawns them.
    public sealed class MazeGhostsTests
    {
        const string GhostFolder = "Assets/_Project/Prefabs/Ghosts/";
        static readonly string[] GhostNames = { "Blinky", "Pinky", "Inky", "Clyde" };

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

        static GameObject[] LoadGhostPrefabs()
        {
            var prefabs = new GameObject[GhostNames.Length];
            for (int i = 0; i < GhostNames.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{GhostFolder}Ghost_{GhostNames[i]}.prefab");
                Assert.That(prefabs[i], Is.Not.Null, $"Missing {GhostFolder}Ghost_{GhostNames[i]}.prefab");
            }

            return prefabs;
        }

        // A maze with its NavMesh and ghost spawner, wired the way the setup menu wires the scene.
        MazeBuilder MakeBuilder(bool spawnGhosts, out GhostMazeLayout layout)
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(Small));
            SetField(config, "ghostPrefabs", LoadGhostPrefabs());

            var layoutObject = new GameObject("Test Ghost Layout");
            created.Add(layoutObject);
            layout = layoutObject.AddComponent<GhostMazeLayout>();

            var go = new GameObject("Test Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin; // far from anything a previous test or scene left behind
            var builder = go.AddComponent<MazeBuilder>();
            go.AddComponent<MazeNavMesh>();
            var ghosts = go.AddComponent<MazeGhosts>();

            SetField(builder, "config", config);
            SetField(ghosts, "layout", layout);
            SetField(ghosts, "houseExit", Marker("House Exit", layoutObject.transform));
            SetField(ghosts, "houseInside", Marker("House Inside", layoutObject.transform));
            SetField(ghosts, "topLeft", Marker("Top Left", layoutObject.transform));
            SetField(ghosts, "topRight", Marker("Top Right", layoutObject.transform));
            SetField(ghosts, "bottomLeft", Marker("Bottom Left", layoutObject.transform));
            SetField(ghosts, "bottomRight", Marker("Bottom Right", layoutObject.transform));
            SetField(ghosts, "spawnGhosts", spawnGhosts);

            go.SetActive(true);
            return builder;
        }

        static Transform Marker(string name, Transform parent)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            return marker;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        static void AssertAt(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.01f), $"{what}: expected {expected}, got {actual}");
        }

        [Test]
        public void TheLayoutIsCompleteAndSitsOnTheRightTiles()
        {
            MazeBuilder builder = MakeBuilder(true, out GhostMazeLayout layout);
            MazeGrid grid = builder.Grid;

            Assert.That(layout.IsComplete, Is.True);
            Assert.That(layout.TileSize, Is.EqualTo(3f).Within(0.001f));
            AssertAt(layout.HouseExit, builder.TileCenter(grid.GhostExit), "house exit");
            AssertAt(layout.HouseInside, builder.TileCenter(MazeGhosts.HouseInsideTile(grid)), "house inside");
            AssertAt(layout.Corner(MazeCorner.TopLeft), builder.TileCenter(1, 1), "top left");
            AssertAt(layout.Corner(MazeCorner.TopRight), builder.TileCenter(1, 7), "top right");
            AssertAt(layout.Corner(MazeCorner.BottomLeft), builder.TileCenter(5, 1), "bottom left");
            AssertAt(layout.Corner(MazeCorner.BottomRight), builder.TileCenter(5, 7), "bottom right");

            // Top is the top of the map, which is the larger z.
            Assert.That(layout.Corner(MazeCorner.TopLeft).z, Is.GreaterThan(layout.Corner(MazeCorner.BottomLeft).z));
        }

        [Test]
        public void SpawnsFourGhosts_BlinkyOutside_TheOthersInTheHouse()
        {
            MazeBuilder builder = MakeBuilder(true, out GhostMazeLayout _);
            MazeGhosts spawner = builder.GetComponent<MazeGhosts>();
            MazeGrid grid = builder.Grid;

            Assert.That(spawner.Spawned.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Ghost ghost = spawner.Spawned[i].GetComponent<Ghost>();
                Assert.That(ghost.Config.DisplayName, Is.EqualTo(GhostNames[i]), "spawn order");
            }

            AssertAt(spawner.Spawned[0].transform.position, builder.TileCenter(grid.GhostExit), "Blinky");

            // The house in the small maze has three tiles, one ghost on each.
            var houseCenters = new List<Vector3>();
            foreach (TileCoord tile in grid.Find(MazeTile.GhostHouse))
                houseCenters.Add(builder.TileCenter(tile));

            var used = new HashSet<Vector3>();
            for (int i = 1; i < 4; i++)
            {
                Vector3 position = spawner.Spawned[i].transform.position;
                Assert.That(houseCenters.Exists(c => Vector3.Distance(c, position) < 0.01f), Is.True, $"{GhostNames[i]} is in the house");
                Assert.That(used.Add(new Vector3(Mathf.Round(position.x), 0f, Mathf.Round(position.z))), Is.True, $"{GhostNames[i]} shares a tile");
            }
        }

        [Test]
        public void TheGhostsStartOnTheNavMesh()
        {
            MazeBuilder builder = MakeBuilder(true, out GhostMazeLayout _);

            foreach (GameObject ghost in builder.GetComponent<MazeGhosts>().Spawned)
                Assert.That(MazeNavMesh.IsOnNavMesh(ghost.transform.position), Is.True, $"{ghost.name} is off the NavMesh");
        }

        [Test]
        public void TurningSpawningOff_SpawnsNoGhosts_ButStillFillsTheLayout()
        {
            MazeBuilder builder = MakeBuilder(false, out GhostMazeLayout layout);

            // Count the ghosts under this maze only: the open scene may have a maze and ghosts of its own.
            Assert.That(builder.GetComponent<MazeGhosts>().Spawned.Count, Is.EqualTo(0));
            Assert.That(builder.GetComponentsInChildren<Ghost>(true).Length, Is.EqualTo(0));
            Assert.That(layout.IsComplete, Is.True);
        }

        [UnityTest]
        public IEnumerator Rebuilding_ReplacesTheGhosts()
        {
            MazeBuilder builder = MakeBuilder(true, out GhostMazeLayout _);

            builder.Build();
            yield return null;

            Assert.That(builder.GetComponent<MazeGhosts>().Spawned.Count, Is.EqualTo(4));
            Assert.That(builder.GetComponentsInChildren<Ghost>().Length, Is.EqualTo(4), "no leftover ghosts under this maze");
        }
    }
}
#endif

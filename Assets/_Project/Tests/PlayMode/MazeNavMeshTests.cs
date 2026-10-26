#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A3: the NavMesh baked from the maze. "A test agent can walk every corridor."
    public sealed class MazeNavMeshTests
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

        MazeBuilder MakeBuilder(string map)
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(map));

            // Inactive while the fields are set, so Awake (which builds and bakes) sees the config.
            var go = new GameObject("Test Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin; // far from anything a previous test or scene left behind
            var builder = go.AddComponent<MazeBuilder>();
            go.AddComponent<MazeNavMesh>();
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

        static string Level1Text()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(asset, Is.Not.Null, $"Missing {Level1Path}");
            return asset.text;
        }

        // Asserts a path from the player start to the middle of every walkable tile.
        static void AssertEveryWalkableTileIsReachable(MazeBuilder builder)
        {
            MazeGrid grid = builder.Grid;
            Vector3 start = builder.TileCenter(grid.PlayerStart);
            var path = new NavMeshPath();

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    if (!grid.IsWalkable(row, col))
                        continue;

                    bool found = NavMesh.CalculatePath(start, builder.TileCenter(row, col), NavMesh.AllAreas, path);
                    Assert.That(found && path.status == NavMeshPathStatus.PathComplete, Is.True,
                        $"No complete path from the player start to tile (row {row}, col {col}), status {path.status}.");
                }
            }
        }

        [Test]
        public void Bakes_OnAwake()
        {
            MazeBuilder builder = MakeBuilder(Small);
            var nav = builder.GetComponent<MazeNavMesh>();

            Assert.That(nav.HasNavMesh, Is.True);
            Assert.That(MazeNavMesh.IsOnNavMesh(builder.TileCenter(builder.Grid.PlayerStart)), Is.True, "player start");
            Assert.That(MazeNavMesh.IsOnNavMesh(builder.TileCenter(builder.Grid.GhostExit)), Is.True, "ghost exit");
        }

        [Test]
        public void EveryWalkableTileIsCovered_AndNoWallTileIs()
        {
            MazeBuilder builder = MakeBuilder(Small);
            MazeGrid grid = builder.Grid;

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    Vector3 center = builder.TileCenter(row, col);
                    bool onMesh = MazeNavMesh.IsOnNavMesh(center);

                    if (grid.IsWalkable(row, col))
                    {
                        Assert.That(onMesh, Is.True, $"walkable tile (row {row}, col {col}) has no NavMesh");
                    }
                    else
                    {
                        Assert.That(onMesh, Is.False, $"wall tile (row {row}, col {col}) has NavMesh on the floor");
                        Assert.That(MazeNavMesh.IsOnNavMesh(center + Vector3.up * 3.3f), Is.False,
                            $"wall tile (row {row}, col {col}) has NavMesh on its top");
                    }
                }
            }
        }

        [Test]
        public void Small_EveryWalkableTileCanBeReached()
        {
            AssertEveryWalkableTileIsReachable(MakeBuilder(Small));
        }

        [Test]
        public void Level1_EveryWalkableTileCanBeReached()
        {
            AssertEveryWalkableTileIsReachable(MakeBuilder(Level1Text()));
        }

        [Test]
        public void GhostsCanWalkThroughTheHouseDoor()
        {
            MazeBuilder builder = MakeBuilder(Small);
            MazeGrid grid = builder.Grid;
            var path = new NavMeshPath();

            // The door tile itself is walkable floor: the door cube is left out of the bake.
            Assert.That(MazeNavMesh.IsOnNavMesh(builder.TileCenter(grid.HouseDoor)), Is.True, "door tile");

            Vector3 inside = builder.TileCenter(MazeGhosts.HouseInsideTile(grid));
            Assert.That(NavMesh.CalculatePath(builder.TileCenter(grid.GhostExit), inside, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "exit to inside the house");

            Assert.That(NavMesh.CalculatePath(inside, builder.TileCenter(grid.GhostExit), NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "inside the house to the exit");
        }

        [UnityTest]
        public IEnumerator ATestAgentWalksFromTheHouseToTheFarCorner()
        {
            MazeBuilder builder = MakeBuilder(Small);
            MazeGrid grid = builder.Grid;

            var agentObject = new GameObject("Test Agent");
            created.Add(agentObject);
            agentObject.transform.position = builder.TileCenter(MazeGhosts.HouseInsideTile(grid));
            var agent = agentObject.AddComponent<NavMeshAgent>();
            agent.speed = 20f;
            agent.acceleration = 100f;
            agent.angularSpeed = 1000f;
            agent.radius = 0.45f;
            agent.height = 1.8f;

            // Row 5, col 7 is the flare tile in the bottom right of the small maze.
            Vector3 target = builder.TileCenter(5, 7);
            Assert.That(agent.isOnNavMesh, Is.True, "the agent starts on the NavMesh");
            Assert.That(agent.SetDestination(target), Is.True);

            float giveUp = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < giveUp && Vector3.Distance(agentObject.transform.position, target) > 1f)
                yield return null;

            Assert.That(Vector3.Distance(agentObject.transform.position, target), Is.LessThan(1f), "the agent reached the far corner");
        }

        [UnityTest]
        public IEnumerator Rebuilding_BakesAgain()
        {
            MazeBuilder builder = MakeBuilder(Small);

            builder.Build();
            yield return null;

            var nav = builder.GetComponent<MazeNavMesh>();
            Assert.That(nav.HasNavMesh, Is.True);
            Assert.That(MazeNavMesh.IsOnNavMesh(builder.TileCenter(builder.Grid.PlayerStart)), Is.True);

            // One surface only: the old geometry (and its surface) was destroyed.
            Assert.That(builder.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>().Length, Is.EqualTo(1));
        }
    }
}
#endif

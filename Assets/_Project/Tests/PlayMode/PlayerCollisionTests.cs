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
    // Task A8 (collision detection): the player's capsule against the maze. The maze is built far
    // from the origin; the player is driven through PlayerController.Step with made-up input and
    // never reads the real keyboard.
    public sealed class PlayerCollisionTests
    {
        // 9 columns by 7 rows. With 3 m tiles: x = col * 3, z = (6 - row) * 3, so the map covers
        // x 0..24 and z 0..18 (tile centres), and the outer walls' inner faces are at x 1.5 / 22.5
        // and z 1.5 / 16.5.
        const string Small =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#...P..o#\n" +
            "#########\n";

        static readonly Vector3 TestOrigin = new Vector3(7000f, 0f, 7000f);

        readonly List<Object> created = new List<Object>();
        GameStateEventChannel states;

        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1f;
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        MazeBuilder MakeMaze(bool withPickups)
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(Small));

            var go = new GameObject("Test Collision Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin;
            var builder = go.AddComponent<MazeBuilder>();
            SetField(builder, "config", config);

            // Pickups with no configs: they are real trigger spheres, but taking one does nothing.
            if (withPickups)
                go.AddComponent<MazePickups>();

            go.SetActive(true);
            return builder;
        }

        // A player on the given tile, facing the given yaw (0 north, 90 east, 180 south, 270 west).
        PlayerController MakePlayer(MazeBuilder maze, TileCoord tile, float yaw)
        {
            states = ScriptableObject.CreateInstance<GameStateEventChannel>();
            created.Add(states);
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            created.Add(config);

            var spawnObject = new GameObject("Test Spawn");
            created.Add(spawnObject);
            spawnObject.transform.SetPositionAndRotation(maze.TileCenter(tile), Quaternion.Euler(0f, yaw, 0f));

            var go = new GameObject("Test Player");
            created.Add(go);
            go.SetActive(false);
            go.tag = "Player";

            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;
            body.slopeLimit = 45f;
            body.skinWidth = 0.05f;

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            var player = go.AddComponent<PlayerController>();
            SetField(player, "config", config);
            SetField(player, "head", head);
            SetField(player, "spawnPoint", spawnObject.transform);
            SetField(player, "gameStateChanged", states);
            go.SetActive(true); // Start moves it to the spawn marker.

            player.InputEnabled = false;
            return player;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        // Walks for the given game time. Calls onFrame after every step.
        static IEnumerator Walk(PlayerController player, Vector2 input, float seconds, System.Action onFrame = null)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                player.Step(input, Vector2.zero);
                elapsed += Time.deltaTime;
                onFrame?.Invoke();
                yield return null;
            }
        }

        IEnumerator StartPlaying()
        {
            states.Raise(GameState.Playing);
            yield return null;
            yield return null; // settle on the floor
        }

        // Where the player is, measured from the maze's own origin.
        static Vector3 Local(PlayerController player) => player.transform.position - TestOrigin;

        [UnityTest]
        public IEnumerator WalkingIntoAWallAtAnAngle_SlidesAlongIt()
        {
            MazeBuilder maze = MakeMaze(false);
            PlayerController player = MakePlayer(maze, maze.Grid.PlayerStart, 45f); // north-east; the wall is to the north
            yield return StartPlaying();

            Vector3 before = Local(player);
            yield return Walk(player, Vector2.up, 1f);
            Vector3 moved = Local(player) - before;

            Assert.That(moved.x, Is.GreaterThan(2.0f), $"it slid east along the wall instead of stopping (moved {moved})");
            Assert.That(moved.z, Is.GreaterThan(0.7f), "it did walk up to the wall first");
            Assert.That(moved.z, Is.LessThan(1.2f), "and it did not go through the wall");
        }

        [UnityTest]
        public IEnumerator TheGhostHouseDoorBlocksThePlayer()
        {
            MazeBuilder maze = MakeMaze(false);
            TileCoord exit = maze.Grid.GhostExit;
            PlayerController player = MakePlayer(maze, exit, 180f); // south, straight at the door
            yield return StartPlaying();

            float doorZ = maze.HouseDoor.position.z - TestOrigin.z; // the door's middle; the face the player meets is 0.15 m in front of it
            float startZ = Local(player).z;
            Assert.That(startZ, Is.GreaterThan(doorZ + 1f), "the player starts above the door");

            yield return Walk(player, Vector2.up, 3f);

            float endZ = Local(player).z;
            Assert.That(endZ, Is.LessThan(startZ - 1f), "it walked up to the door");
            Assert.That(endZ, Is.GreaterThan(doorZ), "and the door stopped it (the ghosts pass through it, the player does not)");
        }

        [UnityTest]
        public IEnumerator PickupsNeverBlockThePlayer()
        {
            MazeBuilder maze = MakeMaze(true);
            var pickups = maze.GetComponent<MazePickups>();
            Assert.That(pickups.Cells.Count, Is.GreaterThan(0), "the corridor has cells in it");
            Assert.That(pickups.Flares.Count, Is.GreaterThan(0), "and a flare at the end");

            PlayerController player = MakePlayer(maze, maze.Grid.PlayerStart, 90f); // east: two cells and a flare ahead
            yield return StartPlaying();

            yield return Walk(player, Vector2.up, 2.5f);

            // The east wall's inner face is at x = 22.5, so the capsule stops at about 22.1.
            Assert.That(Local(player).x, Is.GreaterThan(21.5f), "it walked through the cells and the flare to the end of the corridor");
        }

        [UnityTest]
        public IEnumerator ThePlayerNeverLeavesTheMazeWhateverWayItWalks()
        {
            MazeBuilder maze = MakeMaze(false);
            PlayerController player = MakePlayer(maze, maze.Grid.PlayerStart, 0f);
            yield return StartPlaying();

            var worst = new Vector3(float.MaxValue, 0f, float.MaxValue);
            var best = new Vector3(float.MinValue, 0f, float.MinValue);
            System.Action record = () =>
            {
                Vector3 p = Local(player);
                worst.x = Mathf.Min(worst.x, p.x);
                worst.z = Mathf.Min(worst.z, p.z);
                best.x = Mathf.Max(best.x, p.x);
                best.z = Mathf.Max(best.z, p.z);
            };

            // Eight headings, each walked for a while: into corners, along walls, down the corridors.
            for (int heading = 0; heading < 8; heading++)
            {
                player.Step(Vector2.zero, new Vector2(45f, 0f));
                yield return Walk(player, Vector2.up, 0.8f, record);
            }

            Assert.That(worst.x, Is.GreaterThan(1.5f), $"stayed inside the west wall (lowest x {worst.x})");
            Assert.That(worst.z, Is.GreaterThan(1.5f), $"stayed inside the south wall (lowest z {worst.z})");
            Assert.That(best.x, Is.LessThan(22.5f), $"stayed inside the east wall (highest x {best.x})");
            Assert.That(best.z, Is.LessThan(16.5f), $"stayed inside the north wall (highest z {best.z})");
        }

        [UnityTest]
        public IEnumerator ThePlayerStaysOnTheFloorWhileWalking()
        {
            MazeBuilder maze = MakeMaze(false);
            PlayerController player = MakePlayer(maze, maze.Grid.PlayerStart, 90f);
            yield return StartPlaying();

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            yield return Walk(player, Vector2.up, 1.5f, () =>
            {
                lowest = Mathf.Min(lowest, player.transform.position.y);
                highest = Mathf.Max(highest, player.transform.position.y);
            });

            Assert.That(lowest, Is.GreaterThan(-0.15f), "never sinks into the floor");
            Assert.That(highest, Is.LessThan(0.15f), "never climbs or bounces");
        }
    }
}
#endif

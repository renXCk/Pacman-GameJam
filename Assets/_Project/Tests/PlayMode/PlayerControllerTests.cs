#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Events;
using Game.Maze;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A4: the first-person controller. The tests build a small maze far from the origin
    // (other tests leave colliders near it), drop a player on its start tile and feed it made-up
    // input through PlayerController.Step.
    public sealed class PlayerControllerTests
    {
        // 9 columns by 7 rows. With 3 m tiles: x = col * 3, z = (6 - row) * 3.
        // The player starts at row 5, col 4. North of it is wall, east and west are open.
        const string Small =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#...P..o#\n" +
            "#########\n";

        static readonly Vector3 TestOrigin = new Vector3(3000f, 0f, 3000f);

        readonly List<Object> created = new List<Object>();
        GameStateEventChannel states;
        Transform spawn;

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

        MazeBuilder MakeMaze()
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(Small));

            var go = new GameObject("Test Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin;
            var builder = go.AddComponent<MazeBuilder>();
            SetField(builder, "config", config);
            go.SetActive(true);
            return builder;
        }

        // A player on the start tile, facing the given yaw, with a head and a spawn marker.
        PlayerController MakePlayer(MazeBuilder maze, float yaw)
        {
            states = ScriptableObject.CreateInstance<GameStateEventChannel>();
            created.Add(states);
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            created.Add(config);

            Vector3 start = maze.TileCenter(maze.Grid.PlayerStart);

            var spawnObject = new GameObject("Test Spawn");
            created.Add(spawnObject);
            spawn = spawnObject.transform;
            spawn.SetPositionAndRotation(start, Quaternion.Euler(0f, yaw, 0f));

            var go = new GameObject("Test Player");
            created.Add(go);
            go.SetActive(false);

            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            var player = go.AddComponent<PlayerController>();
            SetField(player, "config", config);
            SetField(player, "head", head);
            SetField(player, "spawnPoint", spawn);
            SetField(player, "gameStateChanged", states);
            go.SetActive(true); // Start moves it to the spawn marker.

            // The tests drive Step themselves. The player must not also read the real keyboard and
            // mouse, or a nudge of the mouse would change the result.
            player.InputEnabled = false;
            return player;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        // Walks for the given number of seconds of game time and returns how long it really took.
        static IEnumerator Walk(PlayerController player, Vector2 input, float seconds, System.Action<float> done)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                player.Step(input, Vector2.zero);
                elapsed += Time.deltaTime;
                yield return null;
            }

            done(elapsed);
        }

        [UnityTest]
        public IEnumerator WalksForwardAtTheConfiguredSpeed()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 90f); // east: nine metres of open corridor
            states.Raise(GameState.Playing);
            yield return null;
            yield return null; // settle on the floor

            Vector3 before = player.transform.position;
            float elapsed = 0f;
            yield return Walk(player, Vector2.up, 1f, e => elapsed = e);

            Vector3 moved = player.transform.position - before;
            Assert.That(moved.x, Is.EqualTo(4.5f * elapsed).Within(0.15f), $"east speed (moved {moved}, game time {elapsed})");
            Assert.That(Mathf.Abs(moved.z), Is.LessThan(0.05f), "no sideways drift");
            Assert.That(Mathf.Abs(moved.y), Is.LessThan(0.15f), "stays on the floor");
        }

        [UnityTest]
        public IEnumerator StrafesAndWalksBackwards()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 90f); // facing east, so right is south (-z)
            states.Raise(GameState.Playing);
            yield return null;
            yield return null;

            Vector3 before = player.transform.position;
            yield return Walk(player, Vector2.down, 0.4f, _ => { });
            Assert.That(player.transform.position.x, Is.LessThan(before.x - 0.5f), "backwards is west");

            before = player.transform.position;
            yield return Walk(player, Vector2.right, 0.3f, _ => { });
            Assert.That(player.transform.position.z, Is.LessThan(before.z), "right of east is south");
        }

        [UnityTest]
        public IEnumerator StopsAtAWall()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 0f); // north: the wall is 1.5 m from the tile centre
            states.Raise(GameState.Playing);
            yield return null;
            yield return null;

            Vector3 before = player.transform.position;
            yield return Walk(player, Vector2.up, 2f, _ => { }); // far more than the gap allows

            float travelled = player.transform.position.z - before.z;
            Assert.That(travelled, Is.GreaterThan(0.7f), "it walked up to the wall");
            Assert.That(travelled, Is.LessThan(1.2f), "it did not go through the wall");
            Assert.That(Mathf.Abs(player.transform.position.x - before.x), Is.LessThan(0.1f));
        }

        [Test]
        public void TurnsWithTheMouse_AndLooksUpAndDownWithALimit()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 0f);

            player.Step(Vector2.zero, new Vector2(90f, 0f));
            Assert.That(Mathf.DeltaAngle(0f, player.transform.eulerAngles.y), Is.EqualTo(90f).Within(0.1f), "turn right");

            player.Step(Vector2.zero, new Vector2(0f, 30f));
            Assert.That(Mathf.DeltaAngle(0f, player.Head.localEulerAngles.x), Is.EqualTo(-30f).Within(0.1f), "look up");

            player.Step(Vector2.zero, new Vector2(0f, 1000f));
            Assert.That(Mathf.DeltaAngle(0f, player.Head.localEulerAngles.x), Is.EqualTo(-80f).Within(0.1f), "up limit");

            player.Step(Vector2.zero, new Vector2(0f, -1000f));
            Assert.That(Mathf.DeltaAngle(0f, player.Head.localEulerAngles.x), Is.EqualTo(80f).Within(0.1f), "down limit");

            // Only the body turns left and right. The head never rolls or yaws on its own.
            Assert.That(player.Head.localEulerAngles.y, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void OnlyMovesWhilePlayingAndNotPaused()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 0f);

            Assert.That(player.CanMove, Is.False, "Boot");
            foreach (GameState blocked in new[] { GameState.Menu, GameState.Ready, GameState.PlayerCaught, GameState.LevelComplete, GameState.GameOver })
            {
                states.Raise(blocked);
                Assert.That(player.CanMove, Is.False, blocked.ToString());
            }

            states.Raise(GameState.Playing);
            Assert.That(player.CanMove, Is.True, "Playing");

            Time.timeScale = 0f; // what the pause menu does
            Assert.That(player.CanMove, Is.False, "paused");
        }

        [UnityTest]
        public IEnumerator ReadyPutsThePlayerBackOnTheSpawnMarker()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 90f);
            states.Raise(GameState.Playing);
            yield return null;
            yield return null;

            yield return Walk(player, Vector2.up, 0.5f, _ => { });
            player.Step(Vector2.zero, new Vector2(40f, 20f));
            Assert.That(Vector3.Distance(player.transform.position, spawn.position), Is.GreaterThan(1f), "it moved away first");

            states.Raise(GameState.PlayerCaught);
            states.Raise(GameState.Ready);
            yield return null;

            Vector3 flat = player.transform.position - spawn.position;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.05f), "back on the start tile");
            Assert.That(Mathf.DeltaAngle(spawn.eulerAngles.y, player.transform.eulerAngles.y), Is.EqualTo(0f).Within(0.1f), "facing the spawn direction");
            Assert.That(player.Pitch, Is.EqualTo(0f), "looking straight ahead");
            Assert.That(player.Head.localEulerAngles.x, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator StartsOnTheSpawnMarkerOnTheFloor()
        {
            MazeBuilder maze = MakeMaze();
            PlayerController player = MakePlayer(maze, 90f);
            yield return null;
            yield return null;

            Vector3 flat = player.transform.position - spawn.position;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.05f));
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.15f), "standing on the floor, not inside it");
        }
    }
}
#endif

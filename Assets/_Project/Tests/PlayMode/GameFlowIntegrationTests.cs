#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using Game.Events;
using Game.Maze;
using Game.Pickups;
using Game.Player;
using Game.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A8: Dev A's pieces working together with Dev B's ghosts and Dev C's game flow, in the real
    // Game scene. Needs every setup menu run: Maze, Player, Flashlight, Pickups, Battery Rules And Warps.
    public sealed class GameFlowIntegrationTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string Events = "Assets/_Project/ScriptableObjects/Events/";
        const string ProgressPath = "Assets/_Project/ScriptableObjects/Runtime/LevelProgress.asset";

        GameManager manager;
        MazeBuilder maze;
        MazePickups pickups;
        PlayerController player;
        PlayerFlashlight flashlight;
        LevelProgress progress;
        GameRulesConfig rules;
        VoidEventChannel playerCaught;
        VoidEventChannel restartRequested;
        MazeGrid grid;

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        // Loads the Game scene, finds the parts and waits for the first round to start playing.
        IEnumerator LoadAndPlay()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            manager = Object.FindAnyObjectByType<GameManager>();
            maze = Object.FindAnyObjectByType<MazeBuilder>();
            pickups = Object.FindAnyObjectByType<MazePickups>();
            player = Object.FindAnyObjectByType<PlayerController>();
            flashlight = Object.FindAnyObjectByType<PlayerFlashlight>();
            progress = AssetDatabase.LoadAssetAtPath<LevelProgress>(ProgressPath);
            rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            playerCaught = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "PlayerCaught.asset");
            restartRequested = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "RestartRequested.asset");

            Assert.That(manager, Is.Not.Null, "GameManager");
            Assert.That(maze, Is.Not.Null, "MazeBuilder: run Game > Maze > Add Maze To Game Scene");
            Assert.That(pickups, Is.Not.Null, "MazePickups: run Game > Pickups > Add Pickups To Game Scene");
            Assert.That(player, Is.Not.Null, "PlayerController: run Game > Player > Add Player To Game Scene");
            Assert.That(flashlight, Is.Not.Null, "PlayerFlashlight: run Game > Player > Add Flashlight To Game Scene");
            Assert.That(playerCaught, Is.Not.Null, "PlayerCaught channel");
            Assert.That(restartRequested, Is.Not.Null, "RestartRequested channel");

            grid = maze.Grid;

            yield return WaitUntil(() => manager.State == GameState.Playing, rules.ReadySeconds + 3f);
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            yield return null;

            // The tests must not read the keyboard.
            player.InputEnabled = false;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float giveUp = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < giveUp)
                yield return null;
        }

        void TakeEverythingThatCounts()
        {
            Collider body = player.GetComponent<CharacterController>();
            for (int i = 0; i < pickups.Cells.Count; i++)
                pickups.Cells[i].Touch(body);
            for (int i = 0; i < pickups.Flares.Count; i++)
                pickups.Flares[i].Touch(body);
        }

        void AssertEverythingIsBack(string when)
        {
            Assert.That(progress.Collected, Is.EqualTo(0), when + ": nothing counted as taken");
            Assert.That(progress.Total, Is.EqualTo(grid.CompletionTotal), when + ": the level total is the map's cells and flares");
            for (int i = 0; i < pickups.Cells.Count; i++)
                Assert.That(pickups.Cells[i].IsAvailable, Is.True, when + ": " + pickups.Cells[i].name);
            for (int i = 0; i < pickups.Flares.Count; i++)
                Assert.That(pickups.Flares[i].IsAvailable, Is.True, when + ": " + pickups.Flares[i].name);
            for (int i = 0; i < pickups.Batteries.Count; i++)
                Assert.That(pickups.Batteries[i].IsAvailable, Is.False, when + ": batteries wait for the player to clear part of the level");
        }

        [UnityTest]
        public IEnumerator ClearingTheLevel_StartsTheNextOne_WithEveryPickupBack()
        {
            yield return LoadAndPlay();
            Assert.That(manager.LevelIndex, Is.EqualTo(0));

            TakeEverythingThatCounts();

            Assert.That(progress.IsComplete, Is.True, "every cell and flare is taken");
            Assert.That(manager.State, Is.EqualTo(GameState.LevelComplete), "the game manager noticed");

            yield return WaitUntil(() => manager.LevelIndex == 1 && manager.State == GameState.Ready, rules.LevelCompleteSeconds + rules.ReadySeconds + 6f);
            Assert.That(manager.LevelIndex, Is.EqualTo(1), "the next level started");
            Assert.That(manager.State, Is.EqualTo(GameState.Ready));
            yield return null;
            yield return null;

            AssertEverythingIsBack("new level");

            Vector3 offset = player.transform.position - maze.TileCenter(grid.PlayerStart);
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(0.3f), "the player is on the start tile");
        }

        [UnityTest]
        public IEnumerator LosingEveryLife_ThenRetry_StartsAFreshGame()
        {
            yield return LoadAndPlay();

            // Some progress and a drained flashlight, so a fresh game is visible.
            Collider body = player.GetComponent<CharacterController>();
            pickups.Cells[0].Touch(body);
            pickups.Cells[1].Touch(body);
            flashlight.Recharge(-60f);
            Assert.That(flashlight.Fraction, Is.LessThan(0.5f));

            // Get caught until the lives run out. A real ghost may catch the player too: that is
            // fine, it only costs a life sooner.
            for (int attempt = 0; attempt < rules.StartingLives + 3 && manager.State != GameState.GameOver; attempt++)
            {
                yield return WaitUntil(() => manager.State == GameState.Playing || manager.State == GameState.GameOver,
                    rules.CaughtSeconds + rules.ReadySeconds + 6f);

                if (manager.State == GameState.Playing)
                    playerCaught.Raise();
            }

            yield return WaitUntil(() => manager.State == GameState.GameOver, rules.CaughtSeconds + 6f);
            Assert.That(manager.State, Is.EqualTo(GameState.GameOver), "no lives left");
            Assert.That(manager.Lives, Is.EqualTo(0));

            // Retry.
            restartRequested.Raise();
            yield return null;
            yield return null;

            Assert.That(manager.State, Is.EqualTo(GameState.Ready), "a new game is getting ready");
            Assert.That(manager.LevelIndex, Is.EqualTo(0), "back on the first level");
            Assert.That(manager.Lives, Is.EqualTo(rules.StartingLives));
            AssertEverythingIsBack("retry");
            Assert.That(flashlight.Fraction, Is.EqualTo(1f).Within(0.001f), "a new game starts with a full battery");
        }

        [UnityTest]
        public IEnumerator EveryPickupCanBeReachedFromThePlayerStartOnTheNavMesh()
        {
            yield return LoadAndPlay();

            Vector3 start = maze.TileCenter(grid.PlayerStart);
            var path = new NavMeshPath();

            int checkedCount = 0;
            foreach (Pickup pickup in pickups.Cells) { AssertReachable(start, pickup, path); checkedCount++; }
            foreach (Pickup pickup in pickups.Flares) { AssertReachable(start, pickup, path); checkedCount++; }
            foreach (Pickup pickup in pickups.Batteries) { AssertReachable(start, pickup, path); checkedCount++; }

            Assert.That(checkedCount, Is.EqualTo(grid.Count(MazeTile.Cell) + grid.Count(MazeTile.Flare) + grid.Count(MazeTile.Battery)));
        }

        static void AssertReachable(Vector3 start, Pickup pickup, NavMeshPath path)
        {
            Vector3 floor = pickup.transform.position;
            floor.y = 0f;

            bool found = NavMesh.CalculatePath(start, floor, NavMesh.AllAreas, path);
            Assert.That(found && path.status == NavMeshPathStatus.PathComplete, Is.True, $"{pickup.name} cannot be reached on the NavMesh");
        }
    }
}
#endif

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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Plays the real Game scene after Game > Pickups > Add Battery Rules And Warps.
    // Task A7: batteries start hidden and appear as the level is cleared; a catch keeps the cells
    // that were taken and puts the player back on the start tile; a new level brings everything back.
    public sealed class ResetGameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string ProgressPath = "Assets/_Project/ScriptableObjects/Runtime/LevelProgress.asset";
        const string CaughtPath = "Assets/_Project/ScriptableObjects/Events/PlayerCaught.asset";
        const string LevelStartedPath = "Assets/_Project/ScriptableObjects/Events/LevelStarted.asset";
        const string Level1Path = "Assets/_Project/ScriptableObjects/Configs/LevelConfig_Level1.asset";

        static int BatteriesOut(MazePickups pickups)
        {
            int count = 0;
            for (int i = 0; i < pickups.Batteries.Count; i++)
            {
                if (pickups.Batteries[i].IsAvailable)
                    count++;
            }

            return count;
        }

        [UnityTest]
        public IEnumerator GameScene_ACatchKeepsTheCells_BatteriesFollowTheRules_ANewLevelResetsAll()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var maze = Object.FindAnyObjectByType<MazeBuilder>();
            var pickups = Object.FindAnyObjectByType<MazePickups>();
            var director = Object.FindAnyObjectByType<BatteryDirector>();
            var warps = Object.FindAnyObjectByType<MazeWarps>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            var progress = AssetDatabase.LoadAssetAtPath<LevelProgress>(ProgressPath);
            var playerCaught = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(CaughtPath);
            var levelStarted = AssetDatabase.LoadAssetAtPath<LevelConfigEventChannel>(LevelStartedPath);
            var level1 = AssetDatabase.LoadAssetAtPath<LevelConfig>(Level1Path);

            Assert.That(pickups, Is.Not.Null, "MazePickups: run Game > Pickups > Add Pickups To Game Scene");
            Assert.That(director, Is.Not.Null, "BatteryDirector: run Game > Pickups > Add Battery Rules And Warps");
            Assert.That(warps, Is.Not.Null, "MazeWarps: run Game > Pickups > Add Battery Rules And Warps");
            Assert.That(player, Is.Not.Null, "PlayerController");
            Assert.That(level1, Is.Not.Null, "LevelConfig_Level1");
            Assert.That(rules.StartingLives, Is.GreaterThan(1), "the test needs a spare life to survive a catch");

            MazeGrid grid = maze.Grid;
            Assert.That(warps.Doors.Count, Is.EqualTo(grid.Count(MazeTile.WarpDoor)), "one door per warp tile on the map (Level 1 has none)");

            float giveUp = Time.realtimeSinceStartup + rules.ReadySeconds + 3f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            yield return null;

            // The tests must not read the keyboard.
            player.InputEnabled = false;
            Collider body = player.GetComponent<CharacterController>();

            // 1. Batteries start hidden; everything else is out.
            Assert.That(BatteriesOut(pickups), Is.EqualTo(0), "batteries wait for the player to clear part of the level");
            Assert.That(pickups.Batteries.Count, Is.EqualTo(grid.Count(MazeTile.Battery)));
            Assert.That(progress.Total, Is.EqualTo(grid.CompletionTotal));
            Assert.That(progress.Collected, Is.EqualTo(0));

            // 2. Take two cells and a flare.
            Assert.That(pickups.Cells[0].Touch(body), Is.True);
            Assert.That(pickups.Cells[1].Touch(body), Is.True);
            Assert.That(pickups.Flares[0].Touch(body), Is.True);
            Assert.That(progress.Collected, Is.EqualTo(3));

            // 3. A catch. The game goes PlayerCaught and then Ready again.
            playerCaught.Raise();
            Assert.That(manager.State, Is.EqualTo(GameState.PlayerCaught));

            float readyGiveUp = Time.realtimeSinceStartup + rules.CaughtSeconds + rules.ReadySeconds + 6f;
            while (manager.State != GameState.Ready && Time.realtimeSinceStartup < readyGiveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Ready), "the player has a life left, so the round restarts");
            yield return null;
            yield return null;

            Assert.That(progress.Collected, Is.EqualTo(3), "a catch keeps the cells that were taken");
            Assert.That(pickups.Cells[0].IsCollected, Is.True);
            Assert.That(pickups.Cells[1].IsCollected, Is.True);
            Assert.That(pickups.Flares[0].IsCollected, Is.True);

            Vector3 start = maze.TileCenter(grid.PlayerStart);
            Vector3 offset = player.transform.position - start;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(0.3f), "the player is back on the start tile");

            // 4. Clear about a third of the level: a battery appears.
            int needed = BatteryRules.Required(0.3f, progress.Total);
            for (int i = 0; i < pickups.Cells.Count && progress.Collected < needed; i++)
                pickups.Cells[i].Touch(body);

            Assert.That(progress.Collected, Is.EqualTo(needed));
            Assert.That(BatteriesOut(pickups), Is.GreaterThanOrEqualTo(1), "the first battery is out after a third of the level");
            Assert.That(progress.IsComplete, Is.False);

            // 5. A new level brings every pickup back and hides the batteries again.
            levelStarted.Raise(level1);

            Assert.That(progress.Collected, Is.EqualTo(0));
            Assert.That(progress.Total, Is.EqualTo(grid.CompletionTotal));
            Assert.That(BatteriesOut(pickups), Is.EqualTo(0));
            Assert.That(director.SpawnedThisLevel, Is.EqualTo(0));
            for (int i = 0; i < pickups.Cells.Count; i++)
                Assert.That(pickups.Cells[i].IsAvailable, Is.True, pickups.Cells[i].name);
            for (int i = 0; i < pickups.Flares.Count; i++)
                Assert.That(pickups.Flares[i].IsAvailable, Is.True, pickups.Flares[i].name);
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;
    }
}
#endif

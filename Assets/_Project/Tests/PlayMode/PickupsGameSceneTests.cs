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
    // Plays the real Game scene after Game > Pickups > Add Pickups To Game Scene.
    public sealed class PickupsGameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string ProgressPath = "Assets/_Project/ScriptableObjects/Runtime/LevelProgress.asset";
        const string PickupEventPath = "Assets/_Project/ScriptableObjects/Events/PickupCollected.asset";
        const string FlareEventPath = "Assets/_Project/ScriptableObjects/Events/FlareCollected.asset";

        [UnityTest]
        public IEnumerator GameScene_HasPickupsFromTheMap_AndWalkingIntoACellTakesIt()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var maze = Object.FindAnyObjectByType<MazeBuilder>();
            var pickups = Object.FindAnyObjectByType<MazePickups>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            var progress = AssetDatabase.LoadAssetAtPath<LevelProgress>(ProgressPath);
            var pickupEvent = AssetDatabase.LoadAssetAtPath<PickupEventChannel>(PickupEventPath);
            var flareEvent = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(FlareEventPath);

            Assert.That(pickups, Is.Not.Null, "MazePickups: run Game > Pickups > Add Pickups To Game Scene");
            Assert.That(maze, Is.Not.Null, "MazeBuilder");
            Assert.That(player, Is.Not.Null, "PlayerController");

            MazeGrid grid = maze.Grid;
            Assert.That(pickups.Cells.Count, Is.EqualTo(grid.Count(MazeTile.Cell)));
            Assert.That(pickups.Flares.Count, Is.EqualTo(grid.Count(MazeTile.Flare)));
            Assert.That(pickups.Batteries.Count, Is.EqualTo(grid.Count(MazeTile.Battery)));

            // The game starts the first level on its own.
            float giveUp = Time.realtimeSinceStartup + rules.ReadySeconds + 3f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            yield return null;

            Assert.That(progress.Total, Is.EqualTo(grid.CompletionTotal), "LevelProgress was started with the cells and flares on the map");
            Assert.That(progress.Collected, Is.EqualTo(0));
            Assert.That(pickups.Cells[0].IsAvailable, Is.True);

            int heardCells = 0;
            int heardFlares = 0;
            System.Action<PickupConfig> onPickup = config => heardCells++;
            System.Action onFlare = () => heardFlares++;
            pickupEvent.Raised += onPickup;
            flareEvent.Raised += onFlare;

            // The tests must not read the keyboard while the player is parked on a cell.
            player.InputEnabled = false;

            // Stand on the cell nearest the player, and let the real physics do the rest.
            Pickup target = pickups.Cells[0];
            float best = float.MaxValue;
            for (int i = 0; i < pickups.Cells.Count; i++)
            {
                float distance = (pickups.Cells[i].transform.position - player.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = pickups.Cells[i];
                }
            }

            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = target.transform.position - Vector3.up * MazePickups.CellHeight + Vector3.up * 0.05f;
            body.enabled = true;

            float walkGiveUp = Time.realtimeSinceStartup + 3f;
            while (!target.IsCollected && Time.realtimeSinceStartup < walkGiveUp)
                yield return null;

            pickupEvent.Raised -= onPickup;
            flareEvent.Raised -= onFlare;

            Assert.That(target.IsCollected, Is.True, "standing on a cell takes it");
            Assert.That(heardCells, Is.EqualTo(1), "PickupCollected was raised once");
            Assert.That(heardFlares, Is.EqualTo(0));
            Assert.That(progress.Collected, Is.EqualTo(1));
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;
    }
}
#endif

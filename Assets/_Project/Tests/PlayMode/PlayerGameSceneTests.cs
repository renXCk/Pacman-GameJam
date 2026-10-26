#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using Game.Ghosts;
using Game.Maze;
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
    // Plays the real Game scene after Game > Player > Add Player To Game Scene.
    public sealed class PlayerGameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string AnchorPath = "Assets/_Project/ScriptableObjects/Runtime/PlayerAnchor.asset";

        [UnityTest]
        public IEnumerator GameScene_HasAPlayerOnTheStartTile_ThatTheGhostsCanSee()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var maze = Object.FindAnyObjectByType<MazeBuilder>();
            var director = Object.FindAnyObjectByType<GhostDirector>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            var anchor = AssetDatabase.LoadAssetAtPath<TransformAnchor>(AnchorPath);

            Assert.That(player, Is.Not.Null, "PlayerController: run Game > Player > Add Player To Game Scene");
            Assert.That(maze, Is.Not.Null, "MazeBuilder");
            Assert.That(director, Is.Not.Null, "GhostDirector");

            Assert.That(player.CompareTag("Player"), Is.True, "the ghosts only catch the Player tag");
            Assert.That(anchor.IsSet, Is.True, "the player is registered on the PlayerAnchor");
            Assert.That(anchor.Value, Is.EqualTo(player.transform));
            Assert.That(director.HasPlayer, Is.True, "the ghost director can see the player");

            Assert.That(Camera.main, Is.Not.Null, "there is a main camera");
            Assert.That(Camera.main.transform.IsChildOf(player.transform), Is.True, "and it belongs to the player");
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1), "one audio listener");
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1), "no leftover placeholder camera");

            float giveUp = Time.realtimeSinceStartup + rules.ReadySeconds + 3f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            yield return null;

            Vector3 start = maze.TileCenter(maze.Grid.PlayerStart);
            Vector3 offset = player.transform.position - start;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(0.2f), "the player stands on the maze's start tile");
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.15f), "on the floor");
            Assert.That(player.CanMove, Is.True);
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;
    }
}
#endif

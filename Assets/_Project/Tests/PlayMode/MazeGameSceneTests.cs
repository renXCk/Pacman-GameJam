#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using Game.Ghosts;
using Game.Maze;
using Game.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Plays the real Game scene after Game > Maze > Add Maze To Game Scene: the maze builds
    // before the first round, the ghost director finds its layout and the ghosts start walking.
    public sealed class MazeGameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [UnityTest]
        public IEnumerator GameScene_BuildsTheMaze_AndTheGhostsStartWalking()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var builder = Object.FindAnyObjectByType<MazeBuilder>();
            var director = Object.FindAnyObjectByType<GhostDirector>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            Assert.That(manager, Is.Not.Null, "GameManager");
            Assert.That(builder, Is.Not.Null, "MazeBuilder: run Game > Maze > Add Maze To Game Scene");
            Assert.That(director, Is.Not.Null, "GhostDirector: run Game > Maze > Add Maze To Game Scene");

            Assert.That(builder.IsBuilt, Is.True, "the maze is built in Awake");
            Assert.That(director.Layout.IsComplete, Is.True);
            Assert.That(director.enabled, Is.True, "the director found a complete layout and stayed enabled");

            Ghost[] ghosts = Object.FindObjectsByType<Ghost>(FindObjectsInactive.Exclude);
            Assert.That(ghosts.Length, Is.EqualTo(4));

            float giveUp = Time.realtimeSinceStartup + rules.ReadySeconds + 3f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(director.IsRunning, Is.True);

            Ghost blinky = null;
            foreach (Ghost ghost in ghosts)
            {
                Assert.That(ghost.Director, Is.EqualTo(director), $"{ghost.name} is bound to the director");
                Assert.That(ghost.IsFrozen, Is.False, $"{ghost.name} is running");
                if (ghost.Config.DisplayName == "Blinky")
                    blinky = ghost;
            }

            Assert.That(blinky, Is.Not.Null);
            Vector3 start = blinky.transform.position;
            yield return new WaitForSeconds(2.5f);

            Assert.That(blinky.HasDestination, Is.True, "Blinky picked a destination");
            Assert.That(Vector3.Distance(start, blinky.transform.position), Is.GreaterThan(1f), "Blinky walked");
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using Game.Events;
using Game.Systems;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Plays the real Game scene with its real assets and no maze, the way Devs A, B and D will
    // press Play in it: it must reach Playing on its own, pause, and end in Game Over after
    // three catches, with no errors in the Console (the test runner fails on any error log).
    public class GameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string EventFolder = "Assets/_Project/ScriptableObjects/Events/";

        [UnityTest]
        public IEnumerator GameSceneRunsOnItsOwnFromReadyToGameOver()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindFirstObjectByType<GameManager>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            var playerCaught = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(EventFolder + "PlayerCaught.asset");
            var pauseRequested = AssetDatabase.LoadAssetAtPath<BoolEventChannel>(EventFolder + "PauseRequested.asset");
            Assert.That(manager, Is.Not.Null);

            yield return WaitFor(manager, GameState.Playing, rules.ReadySeconds + 2f);
            Assert.That(Text("Level").text, Is.EqualTo("LEVEL 1"));
            Assert.That(Text("State Message").enabled, Is.False, "nothing covers the maze while playing");

            pauseRequested.Raise(true);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(GameObject.Find("Game Canvas/Pause Menu/Panel"), Is.Not.Null, "pause menu is showing");
            pauseRequested.Raise(false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            for (int i = 0; i < rules.StartingLives; i++)
            {
                yield return WaitFor(manager, GameState.Playing, rules.CaughtSeconds + rules.ReadySeconds + 2f);
                playerCaught.Raise();
                Assert.That(Text("State Message").text, Is.EqualTo("CAUGHT"));
            }

            yield return WaitFor(manager, GameState.GameOver, rules.CaughtSeconds + 2f);
            yield return new WaitForSecondsRealtime(rules.GameOverSeconds + 0.5f);
            Assert.That(GameObject.Find("Game Canvas/Game Over/Panel"), Is.Not.Null, "Retry / Main Menu screen is showing");
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        static TMP_Text Text(string name)
        {
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.name == name)
                    return text;
            }

            Assert.Fail($"No text called {name} in the scene.");
            return null;
        }

        static IEnumerator WaitFor(GameManager manager, GameState state, float seconds)
        {
            float giveUp = Time.realtimeSinceStartup + seconds;
            while (manager.State != state && Time.realtimeSinceStartup < giveUp)
                yield return null;

            Assert.That(manager.State, Is.EqualTo(state));
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using Game.Events;
using Game.Lighting;
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
    // Plays the real Game scene after Game > Player > Add Flashlight To Game Scene.
    public sealed class FlashlightGameSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string BatteryPath = "Assets/_Project/ScriptableObjects/Events/BatteryChanged.asset";

        [UnityTest]
        public IEnumerator GameScene_FlashlightShinesAndTheBatteryDrains()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var flashlight = Object.FindAnyObjectByType<PlayerFlashlight>();
            var beam = Object.FindAnyObjectByType<FlashlightController>();
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/_Project/ScriptableObjects/Configs/GameRulesConfig.asset");
            var battery = AssetDatabase.LoadAssetAtPath<FloatEventChannel>(BatteryPath);

            Assert.That(flashlight, Is.Not.Null, "PlayerFlashlight: run Game > Player > Add Flashlight To Game Scene");
            Assert.That(beam, Is.Not.Null, "FlashlightController");
            Assert.That(battery.ListenerCount, Is.GreaterThanOrEqualTo(1), "Dev D's flashlight listens to BatteryChanged");
            Assert.That(beam.LightComponent.type, Is.EqualTo(LightType.Spot));

            float giveUp = Time.realtimeSinceStartup + rules.ReadySeconds + 3f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));

            yield return new WaitForSeconds(0.5f);
            Assert.That(flashlight.IsOn, Is.True, "the light is on while playing");
            Assert.That(beam.IsShining, Is.True, "Dev D's controller is shining it");

            float before = flashlight.Fraction;
            yield return new WaitForSeconds(2f);
            Assert.That(flashlight.Fraction, Is.LessThan(before - 0.01f), "the battery drains while the light is on");
            Assert.That(flashlight.Fraction, Is.GreaterThan(0f));

            // Switching it off stops the drain.
            flashlight.Toggle();
            float off = flashlight.Fraction;
            yield return new WaitForSeconds(1f);
            Assert.That(flashlight.Fraction, Is.EqualTo(off).Within(0.0001f));
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;
    }
}
#endif

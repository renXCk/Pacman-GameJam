#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Events;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A6: the flashlight's switch and battery. The tests drive PlayerFlashlight.Step with
    // made-up frame times, so they take no real time. With the test config the battery is 100,
    // it drains 10 per second (so 10 percent a second) and a pickup gives 50.
    public sealed class PlayerFlashlightTests
    {
        readonly List<Object> created = new List<Object>();

        sealed class Rig
        {
            public PlayerFlashlight Flashlight;
            public Light Light;
            public GameObject Owner;
            public FloatEventChannel Battery;
            public GameStateEventChannel States;
            public VoidEventChannel NewGame;
            public readonly List<float> Raised = new List<float>();

            public float LastRaised => Raised[Raised.Count - 1];
        }

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

        Rig Make(bool activate = true)
        {
            var rig = new Rig();

            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            created.Add(config);
            SetField(config, "flashlightMaxBattery", 100f);
            SetField(config, "drainPerSecond", 10f);
            SetField(config, "rechargeAmount", 50f);
            SetField(config, "batteryEmptyThreshold", 0.02f);

            rig.Battery = ScriptableObject.CreateInstance<FloatEventChannel>();
            rig.States = ScriptableObject.CreateInstance<GameStateEventChannel>();
            rig.NewGame = ScriptableObject.CreateInstance<VoidEventChannel>();
            created.Add(rig.Battery);
            created.Add(rig.States);
            created.Add(rig.NewGame);
            rig.Battery.Raised += rig.Raised.Add;

            var lightObject = new GameObject("Test Flashlight");
            created.Add(lightObject);
            rig.Light = lightObject.AddComponent<Light>();
            rig.Light.type = LightType.Spot;

            // Inactive while the fields are set, so Awake sees the config.
            rig.Owner = new GameObject("Test Player");
            created.Add(rig.Owner);
            rig.Owner.SetActive(false);
            rig.Flashlight = rig.Owner.AddComponent<PlayerFlashlight>();
            SetField(rig.Flashlight, "config", config);
            SetField(rig.Flashlight, "flashlight", rig.Light);
            SetField(rig.Flashlight, "gameStateChanged", rig.States);
            SetField(rig.Flashlight, "newGameStarted", rig.NewGame);
            SetField(rig.Flashlight, "batteryChanged", rig.Battery);

            if (activate)
                rig.Owner.SetActive(true);
            return rig;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator StartsFull_AndAnnouncesTheChargeOnStart()
        {
            Rig rig = Make();
            yield return null; // Start runs

            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(1f));
            Assert.That(rig.Raised.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(rig.Raised[0], Is.EqualTo(1f));
            Assert.That(rig.Flashlight.IsOn, Is.True, "the light starts on");
        }

        [Test]
        public void DrainsOnlyWhileTheLightIsOnAndTheGameIsPlaying()
        {
            Rig rig = Make();

            rig.Flashlight.Step(2f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(1f), "not Playing yet");

            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Step(2f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.8f).Within(0.0001f), "two seconds at 10 percent a second");

            rig.Light.enabled = false;
            rig.Flashlight.Step(5f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.8f).Within(0.0001f), "off does not drain");

            rig.Light.enabled = true;
            rig.States.Raise(GameState.PlayerCaught);
            rig.Flashlight.Step(5f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.8f).Within(0.0001f), "caught does not drain");
        }

        [Test]
        public void ToggleSwitchesTheLight()
        {
            Rig rig = Make();

            Assert.That(rig.Flashlight.Toggle(), Is.False);
            Assert.That(rig.Light.enabled, Is.False);
            Assert.That(rig.Flashlight.Toggle(), Is.True);
            Assert.That(rig.Light.enabled, Is.True);
        }

        [Test]
        public void GoesOutByItselfWhenTheBatteryIsEmpty()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);

            rig.Flashlight.Step(9f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(rig.Flashlight.IsOn, Is.True, "still on at 10 percent");

            rig.Flashlight.Step(0.85f); // 1.5 percent: below the 2 percent threshold
            Assert.That(rig.Flashlight.IsEmpty, Is.True);
            Assert.That(rig.Flashlight.IsOn, Is.False, "the light went out");
            Assert.That(rig.LastRaised, Is.EqualTo(rig.Flashlight.Fraction).Within(0.0001f), "the HUD heard it");

            Assert.That(rig.Flashlight.Toggle(), Is.False, "a dead battery cannot be switched on");
            Assert.That(rig.Flashlight.IsOn, Is.False);
        }

        [Test]
        public void TheChargeNeverGoesBelowZero()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);

            rig.Flashlight.Step(100f);

            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0f));
            Assert.That(rig.LastRaised, Is.EqualTo(0f));
        }

        [Test]
        public void ARechargeRestoresTheChargeAndRelightsADeadLight()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Step(100f);
            Assert.That(rig.Flashlight.IsOn, Is.False);

            rig.Flashlight.Recharge();

            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.5f).Within(0.0001f), "one pickup is 50");
            Assert.That(rig.Flashlight.IsOn, Is.True, "it comes back on by itself");
            Assert.That(rig.LastRaised, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ARechargeNeverGoesPastFull_AndKeepsAManualOffSwitch()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);

            rig.Flashlight.Recharge(500f);
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(1f));

            // Switched off by the player (not by an empty battery): a pickup does not turn it on.
            rig.Flashlight.Toggle();
            rig.Flashlight.Step(3f);
            rig.Flashlight.Recharge(10f);
            Assert.That(rig.Flashlight.IsOn, Is.False);
        }

        [Test]
        public void AnnouncesOnlyChangesOfHalfAPercentOrMore()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Recharge(0f); // announces the current value, so there is something to compare with
            rig.Raised.Clear();

            rig.Flashlight.Step(0.01f); // a tenth of a percent
            Assert.That(rig.Raised.Count, Is.EqualTo(0), "too small to announce");

            rig.Flashlight.Step(0.1f); // now about 1.1 percent below the last announcement
            Assert.That(rig.Raised.Count, Is.EqualTo(1));
            Assert.That(rig.LastRaised, Is.EqualTo(0.989f).Within(0.0005f));
        }

        [Test]
        public void ANewGameRefillsTheBatteryAndSwitchesTheLightOn()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Step(5f);
            rig.Flashlight.Toggle();
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.5f).Within(0.0001f));

            rig.NewGame.Raise();

            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(1f));
            Assert.That(rig.Flashlight.IsOn, Is.True);
            Assert.That(rig.LastRaised, Is.EqualTo(1f));
        }

        [Test]
        public void EveryRoundStartsWithTheLightOn_IfThereIsBattery()
        {
            Rig rig = Make();
            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Step(2f);
            rig.Flashlight.Toggle(); // the player switched it off

            rig.States.Raise(GameState.PlayerCaught);
            rig.States.Raise(GameState.Ready);

            Assert.That(rig.Flashlight.IsOn, Is.True, "the light is on again");
            Assert.That(rig.Flashlight.Fraction, Is.EqualTo(0.8f).Within(0.0001f), "a catch does not refill the battery");
            Assert.That(rig.LastRaised, Is.EqualTo(0.8f).Within(0.0001f), "the HUD is in sync");

            // A dead battery stays dark on Ready.
            rig.States.Raise(GameState.Playing);
            rig.Flashlight.Step(100f);
            rig.States.Raise(GameState.Ready);
            Assert.That(rig.Flashlight.IsOn, Is.False);
        }

        [Test]
        public void CanOnlyBeToggledWhilePlayingAndNotPaused()
        {
            Rig rig = Make();

            Assert.That(rig.Flashlight.CanToggle, Is.False, "Boot");
            rig.States.Raise(GameState.Ready);
            Assert.That(rig.Flashlight.CanToggle, Is.False, "Ready");

            rig.States.Raise(GameState.Playing);
            Assert.That(rig.Flashlight.CanToggle, Is.True, "Playing");

            Time.timeScale = 0f;
            Assert.That(rig.Flashlight.CanToggle, Is.False, "paused");
        }
    }
}
#endif

using Game.Core;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    // Sanity checks on the real config assets, so a bad edit in the Inspector fails a test
    // instead of a playtest.
    public class ConfigAssetTests
    {
        const string Configs = "Assets/_Project/ScriptableObjects/Configs/";

        [Test]
        public void EveryLevelInTheSequenceIsPlayable()
        {
            var sequence = Load<LevelSequence>("LevelSequence");
            Assert.That(sequence.Count, Is.GreaterThan(0));

            for (int i = 0; i < sequence.Count; i++)
            {
                LevelConfig level = sequence.Get(i);
                Assert.That(level, Is.Not.Null, $"level {i + 1} is empty");
                Assert.That(level.GhostSpeed, Is.GreaterThan(0f), level.name);
                Assert.That(level.FrightenedSpeed, Is.GreaterThan(0f), level.name);
                Assert.That(level.EatenSpeed, Is.GreaterThan(0f), level.name);
                Assert.That(level.BatteryPoints, Is.GreaterThanOrEqualTo(0), level.name);
            }
        }

        [Test]
        public void LevelsGetHarder()
        {
            var sequence = Load<LevelSequence>("LevelSequence");
            for (int i = 1; i < sequence.Count; i++)
            {
                Assert.That(sequence.Get(i).GhostSpeed, Is.GreaterThanOrEqualTo(sequence.Get(i - 1).GhostSpeed));
                Assert.That(sequence.Get(i).BatteryPoints, Is.GreaterThanOrEqualTo(sequence.Get(i - 1).BatteryPoints));
            }
        }

        [Test]
        public void PickupsMatchTheOriginalPoints()
        {
            Assert.That(Load<PickupConfig>("PickupConfig_Cell").BasePoints, Is.EqualTo(10));
            Assert.That(Load<PickupConfig>("PickupConfig_Flare").BasePoints, Is.EqualTo(50));
            Assert.That(Load<PickupConfig>("PickupConfig_Battery").Kind, Is.EqualTo(PickupKind.Battery));
            Assert.That(Load<PickupConfig>("PickupConfig_Battery").CountsTowardLevel, Is.False);
        }

        [Test]
        public void GameRulesAreSane()
        {
            var rules = Load<GameRulesConfig>("GameRulesConfig");
            Assert.That(rules.StartingLives, Is.EqualTo(3));
            Assert.That(rules.ExtraLifeScore, Is.EqualTo(10000));
            Assert.That(rules.MaxDisplayedLives, Is.GreaterThanOrEqualTo(rules.StartingLives));
            Assert.That(rules.MenuScene, Is.Not.Empty);
            Assert.That(rules.GameScene, Is.Not.Empty);
        }

        static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Configs + name + ".asset");
            Assert.That(asset, Is.Not.Null, $"{name}.asset is missing. Run Game > Systems > Create Systems Assets.");
            return asset;
        }
    }
}

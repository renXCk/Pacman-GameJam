using Game.Core;
using Game.Ghosts;
using Game.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Score, lives, level progress and level order: the plain C# rules behind the systems.
    public class SystemsRulesTests
    {
        [Test]
        public void PickupsScoreFromTheirConfigAndBatteriesFromTheLevel()
        {
            var cell = Pickup(PickupKind.Cell, 10);
            var flare = Pickup(PickupKind.Flare, 50);
            var battery = Pickup(PickupKind.Battery, 100);
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            Set(level, so => so.FindProperty("batteryPoints").intValue = 700);

            Assert.That(cell.PointsFor(level), Is.EqualTo(10));
            Assert.That(flare.PointsFor(level), Is.EqualTo(50));
            Assert.That(battery.PointsFor(level), Is.EqualTo(700), "batteries use the level's points");
            Assert.That(battery.PointsFor(null), Is.EqualTo(100), "without a level the base points are the fallback");

            Destroy(cell, flare, battery, level);
        }

        [Test]
        public void ScoreAddsEverySourceAndPassesBanishPointsThrough()
        {
            var ai = ScriptableObject.CreateInstance<GhostAIConfig>();
            var score = new ScoreKeeper(10000);

            score.Add(10);
            score.Add(50);
            score.Add(ai.BanishPoints(1));
            score.Add(ai.BanishPoints(2));

            Assert.That(score.Score, Is.EqualTo(10 + 50 + 200 + 400));
            Destroy(ai);
        }

        [Test]
        public void ExtraLifeIsAwardedExactlyOnceAtTheThreshold()
        {
            var score = new ScoreKeeper(10000);

            Assert.That(score.Add(9990), Is.False, "below the threshold");
            Assert.That(score.Add(10), Is.True, "reaching it exactly awards the life");
            Assert.That(score.Add(20000), Is.False, "only once per game");
            Assert.That(score.ExtraLifeAwarded, Is.True);

            score.Reset();
            Assert.That(score.Score, Is.EqualTo(0));
            Assert.That(score.Add(10000), Is.True, "a new game can earn it again");
        }

        [Test]
        public void NoExtraLifeBelowTheThresholdOrWhenTurnedOff()
        {
            var score = new ScoreKeeper(10000);
            Assert.That(score.Add(9999), Is.False);
            Assert.That(score.ExtraLifeAwarded, Is.False);

            var off = new ScoreKeeper(0);
            Assert.That(off.Add(50000), Is.False);
        }

        [Test]
        public void NegativeOrZeroPointsAreIgnored()
        {
            var score = new ScoreKeeper(100);
            score.Add(0);
            score.Add(-50);
            Assert.That(score.Score, Is.EqualTo(0));
        }

        [Test]
        public void LivesNeverGoBelowZero()
        {
            var lives = new LivesKeeper();
            lives.Reset(3);

            lives.Lose();
            lives.Lose();
            lives.Gain();
            Assert.That(lives.Lives, Is.EqualTo(2));

            lives.Lose();
            lives.Lose();
            lives.Lose();
            Assert.That(lives.Lives, Is.EqualTo(0));
            Assert.That(lives.IsOut, Is.True);
        }

        [Test]
        public void LevelProgressCompletesOncePerBegin()
        {
            var counter = new LevelProgressCounter();
            counter.Begin(3);

            Assert.That(counter.Collect(), Is.False);
            Assert.That(counter.Collect(), Is.False);
            Assert.That(counter.Collect(), Is.True, "the last cell completes the level");
            Assert.That(counter.Collect(), Is.False, "never twice");
            Assert.That(counter.Remaining, Is.EqualTo(0));

            counter.Begin(2);
            Assert.That(counter.IsComplete, Is.False, "Begin resets");
            Assert.That(counter.Remaining, Is.EqualTo(2));
        }

        [Test]
        public void LevelProgressWithNoCellsNeverCompletes()
        {
            var counter = new LevelProgressCounter();
            counter.Begin(0);
            Assert.That(counter.Collect(), Is.False);
            Assert.That(counter.IsComplete, Is.False);
        }

        [Test]
        public void LevelProgressAssetRaisesCompletedOnce()
        {
            var progress = ScriptableObject.CreateInstance<LevelProgress>();
            int completed = 0, changed = 0;
            progress.Completed += () => completed++;
            progress.Changed += _ => changed++;

            progress.Begin(2);
            progress.Collect();
            progress.Collect();
            progress.Collect();

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(4), "Begin and every Collect");
            Destroy(progress);
        }

        [Test]
        public void LevelSequenceClampsPastTheEnd()
        {
            var first = ScriptableObject.CreateInstance<LevelConfig>();
            var last = ScriptableObject.CreateInstance<LevelConfig>();
            var sequence = ScriptableObject.CreateInstance<LevelSequence>();
            Set(sequence, so =>
            {
                SerializedProperty levels = so.FindProperty("levels");
                levels.arraySize = 2;
                levels.GetArrayElementAtIndex(0).objectReferenceValue = first;
                levels.GetArrayElementAtIndex(1).objectReferenceValue = last;
            });

            Assert.That(sequence.Get(0), Is.SameAs(first));
            Assert.That(sequence.Get(1), Is.SameAs(last));
            Assert.That(sequence.Get(7), Is.SameAs(last), "past the end the last level repeats");
            Assert.That(sequence.Get(-1), Is.SameAs(first));

            var empty = ScriptableObject.CreateInstance<LevelSequence>();
            Assert.That(empty.Get(0), Is.Null);

            Destroy(first, last, sequence, empty);
        }

        static PickupConfig Pickup(PickupKind kind, int points)
        {
            var pickup = ScriptableObject.CreateInstance<PickupConfig>();
            Set(pickup, so =>
            {
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("basePoints").intValue = points;
            });
            return pickup;
        }

        static void Set(Object target, System.Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Destroy(params Object[] objects)
        {
            foreach (Object item in objects)
                Object.DestroyImmediate(item);
        }
    }
}

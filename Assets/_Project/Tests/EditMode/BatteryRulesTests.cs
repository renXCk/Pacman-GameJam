using Game.Pickups;
using NUnit.Framework;

namespace Game.Tests
{
    // Task A7: when a battery appears.
    public sealed class BatteryRulesTests
    {
        [Test]
        public void ABatteryAppearsWhenAThresholdIsCrossed_AndEachThresholdPaysOnce()
        {
            var rules = new BatteryRules(new[] { 0.25f, 0.5f });

            // Out of 16: a quarter is 4 taken, a half is 8.
            Assert.That(rules.TakeDue(0, 16), Is.EqualTo(0));
            Assert.That(rules.TakeDue(3, 16), Is.EqualTo(0));
            Assert.That(rules.TakeDue(4, 16), Is.EqualTo(1));
            Assert.That(rules.TakeDue(4, 16), Is.EqualTo(0), "the same threshold does not pay twice");
            Assert.That(rules.TakeDue(7, 16), Is.EqualTo(0));
            Assert.That(rules.TakeDue(8, 16), Is.EqualTo(1));
            Assert.That(rules.TakeDue(16, 16), Is.EqualTo(0), "no thresholds left");
            Assert.That(rules.Spawned, Is.EqualTo(2));
        }

        [Test]
        public void JumpingPastSeveralThresholds_PaysAllOfThem()
        {
            var rules = new BatteryRules(new[] { 0.25f, 0.5f, 0.9f });
            Assert.That(rules.TakeDue(10, 16), Is.EqualTo(2));
            Assert.That(rules.TakeDue(16, 16), Is.EqualTo(1));
        }

        [Test]
        public void Begin_MakesEveryThresholdCountAgain()
        {
            var rules = new BatteryRules(new[] { 0.25f });
            Assert.That(rules.TakeDue(16, 16), Is.EqualTo(1));
            Assert.That(rules.TakeDue(16, 16), Is.EqualTo(0));

            rules.Begin();

            Assert.That(rules.Spawned, Is.EqualTo(0));
            Assert.That(rules.TakeDue(4, 16), Is.EqualTo(1));
        }

        [Test]
        public void ThresholdsMayBeGivenInAnyOrder_AndAreKeptBetweenZeroAndOne()
        {
            var rules = new BatteryRules(new[] { 0.75f, 0.25f, 5f });
            Assert.That(rules.ThresholdCount, Is.EqualTo(3));

            // Out of 8: 0.25 needs 2, 0.75 needs 6, and 5 is clamped to 1, which needs all 8.
            Assert.That(rules.TakeDue(1, 8), Is.EqualTo(0));
            Assert.That(rules.TakeDue(2, 8), Is.EqualTo(1));
            Assert.That(rules.TakeDue(6, 8), Is.EqualTo(1));
            Assert.That(rules.TakeDue(7, 8), Is.EqualTo(0));
            Assert.That(rules.TakeDue(8, 8), Is.EqualTo(1));
        }

        [Test]
        public void ABatteryNeverAppearsBeforeSomethingWasTaken_OrWithoutALevel()
        {
            var zero = new BatteryRules(new[] { 0f });
            Assert.That(zero.TakeDue(0, 16), Is.EqualTo(0), "even a threshold of 0 waits for the first pickup");
            Assert.That(zero.TakeDue(1, 16), Is.EqualTo(1));

            var rules = new BatteryRules(new[] { 0.25f });
            Assert.That(rules.TakeDue(5, 0), Is.EqualTo(0), "no level started yet");
        }

        [Test]
        public void NoThresholds_MeansNoBatteries()
        {
            Assert.That(new BatteryRules(new float[0]).TakeDue(16, 16), Is.EqualTo(0));
            Assert.That(new BatteryRules(null).TakeDue(16, 16), Is.EqualTo(0));
        }

        [Test]
        public void Required_RoundsUp_AndIsAtLeastOne()
        {
            Assert.That(BatteryRules.Required(0.25f, 17), Is.EqualTo(5));
            Assert.That(BatteryRules.Required(0.5f, 16), Is.EqualTo(8));
            Assert.That(BatteryRules.Required(0f, 16), Is.EqualTo(1));
            Assert.That(BatteryRules.Required(1f, 16), Is.EqualTo(16));
            Assert.That(BatteryRules.Required(0.2f, 10), Is.EqualTo(2), "0.2 of 10 is 2, not 3");
        }
    }
}

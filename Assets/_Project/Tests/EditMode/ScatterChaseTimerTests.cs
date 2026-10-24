using Game.Ghosts;
using NUnit.Framework;

namespace Game.Tests
{
    public class ScatterChaseTimerTests
    {
        [Test]
        public void FollowsTheScheduleThenChasesForever()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 7f, 20f, 5f });

            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Scatter));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(7f).Within(1e-4));

            Assert.That(timer.Tick(7f), Is.True);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Chase));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(20f).Within(1e-4));

            Assert.That(timer.Tick(20f), Is.True);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Scatter));

            Assert.That(timer.Tick(5f), Is.True);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Chase));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(0f), "the last mode has no end");

            Assert.That(timer.Tick(1000f), Is.False);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Chase));
        }

        [Test]
        public void ALongStepCanCrossSeveralPhases()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 1f, 1f, 1f });

            timer.Tick(2.5f);

            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Scatter));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(0.5f).Within(1e-4));
        }

        [Test]
        public void FrightenedPausesTheScheduleThenResumesIt()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 7f, 20f });
            timer.Tick(3f);

            timer.StartFrightened(6f);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Frightened));
            Assert.That(timer.BaseMode, Is.EqualTo(GhostMode.Scatter));

            Assert.That(timer.Tick(5.9f), Is.False);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Frightened));

            Assert.That(timer.Tick(0.2f), Is.True);
            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Scatter));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(4f).Within(1e-4), "Scatter keeps the 4 s it had left");
        }

        [Test]
        public void ASecondFlareRestartsTheCountdown()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 7f });

            timer.StartFrightened(6f);
            timer.Tick(4f);
            timer.StartFrightened(6f);
            timer.Tick(4f);

            Assert.That(timer.IsFrightened, Is.True);
            Assert.That(timer.FrightenedLeft, Is.EqualTo(2f).Within(1e-4));
        }

        [Test]
        public void ZeroSecondsOfFrightIsIgnored()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 7f });

            timer.StartFrightened(0f);

            Assert.That(timer.IsFrightened, Is.False);
        }

        [Test]
        public void ZeroLengthPhasesAreSkipped()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new[] { 0f });

            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Chase));
            Assert.That(timer.ModeSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void AnEmptyScheduleIsScatterForever()
        {
            var timer = new ScatterChaseTimer();
            timer.Reset(new float[0]);

            timer.Tick(100f);

            Assert.That(timer.Mode, Is.EqualTo(GhostMode.Scatter));
        }
    }
}

using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class SimulationClockTests
    {
        [Test]
        public void FrameLimitPreservesUnprocessedSimulationTime()
        {
            var clock = new SimulationClock(10, 3);
            var totalSteps = 0;

            Assert.That(clock.Advance(1, false, 1, () => totalSteps++), Is.EqualTo(3));
            Assert.That(clock.PendingSeconds, Is.EqualTo(0.7).Within(0.0000001));
            Assert.That(clock.Advance(0, false, 1, () => totalSteps++), Is.EqualTo(3));
            Assert.That(clock.Advance(0, false, 1, () => totalSteps++), Is.EqualTo(3));
            Assert.That(clock.Advance(0, false, 1, () => totalSteps++), Is.EqualTo(1));
            Assert.That(totalSteps, Is.EqualTo(10));
            Assert.That(clock.PendingSeconds, Is.EqualTo(0).Within(0.0000001));
        }

        [Test]
        public void PauseNeitherAddsTimeNorExecutesExistingBacklog()
        {
            var clock = new SimulationClock(10, 1);
            var totalSteps = 0;
            clock.Advance(0.35, false, 1, () => totalSteps++);
            var pendingBeforePause = clock.PendingSeconds;

            Assert.That(clock.Advance(20, true, 4, () => totalSteps++), Is.Zero);
            Assert.That(clock.PendingSeconds, Is.EqualTo(pendingBeforePause));
            Assert.That(totalSteps, Is.EqualTo(1));
            Assert.That(clock.Advance(0, false, 1, () => totalSteps++), Is.EqualTo(1));
        }

        [Test]
        public void SpeedChangesSimulationTimeWithoutChangingStepDuration()
        {
            var clock = new SimulationClock(20, 20);
            var totalSteps = 0;

            Assert.That(clock.Advance(0.25, false, 2, () => totalSteps++), Is.EqualTo(10));
            Assert.That(totalSteps, Is.EqualTo(10));
            Assert.That(clock.PendingSeconds, Is.EqualTo(0).Within(0.0000001));
        }
    }
}

using NUnit.Framework;
using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class AutoOptimizationTriggerTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void BothDisabled_ReturnsNull()
        {
            Assert.IsNull(AutoOptimizationTrigger.Decide(Now, 0, Now.AddDays(-9), 0, 1, Now.AddDays(-9), 5));
        }

        [Test]
        public void IntervalElapsed_ReturnsSchedule()
        {
            Assert.AreEqual(Enums.Memory.Optimization.Reason.Schedule, AutoOptimizationTrigger.Decide(Now, 2, Now.AddHours(-2), 0, 50, Now, 5));
        }

        [Test]
        public void IntervalNotElapsed_ReturnsNull()
        {
            Assert.IsNull(AutoOptimizationTrigger.Decide(Now, 2, Now.AddHours(-1), 0, 50, Now, 5));
        }

        [Test]
        public void LowMemoryAfterCooldown_ReturnsLowMemory()
        {
            Assert.AreEqual(Enums.Memory.Optimization.Reason.LowMemory, AutoOptimizationTrigger.Decide(Now, 0, Now, 20, 10, Now.AddMinutes(-6), 5));
        }

        [Test]
        public void LowMemoryWithinCooldown_ReturnsNull()
        {
            Assert.IsNull(AutoOptimizationTrigger.Decide(Now, 0, Now, 20, 10, Now.AddMinutes(-1), 5));
        }

        [Test]
        public void IntervalWinsOverLowMemory()
        {
            Assert.AreEqual(Enums.Memory.Optimization.Reason.Schedule, AutoOptimizationTrigger.Decide(Now, 1, Now.AddHours(-3), 20, 5, Now.AddHours(-3), 5));
        }
    }
}

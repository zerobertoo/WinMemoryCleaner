using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class AdaptiveCooldownTests
    {
        private const ulong Total = 16UL * 1024 * 1024 * 1024;

        [Test]
        public void Initial_UsesBaseCooldown()
        {
            Assert.AreEqual(5, new AdaptiveCooldown().GetMinutes(5));
        }

        [Test]
        public void NoGain_DoublesUpToCap()
        {
            var cooldown = new AdaptiveCooldown();

            cooldown.Record(0, Total);
            Assert.AreEqual(10, cooldown.GetMinutes(5));

            cooldown.Record(0, Total);
            cooldown.Record(0, Total);
            cooldown.Record(0, Total);
            cooldown.Record(0, Total);

            Assert.AreEqual(40, cooldown.GetMinutes(5));
        }

        [Test]
        public void RealGain_ResetsCooldown()
        {
            var cooldown = new AdaptiveCooldown();

            cooldown.Record(0, Total);
            cooldown.Record(Total / 10, Total);

            Assert.AreEqual(5, cooldown.GetMinutes(5));
        }

        [Test]
        public void UnknownTotal_DoesNotBackOff()
        {
            var cooldown = new AdaptiveCooldown();

            cooldown.Record(0, 0);

            Assert.AreEqual(5, cooldown.GetMinutes(5));
        }
    }
}

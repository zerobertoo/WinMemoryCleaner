using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class OptimizationProfileTests
    {
        private const Enums.Memory.Areas All = (Enums.Memory.Areas)255;

        [TestCase(OptimizationProfile.Kind.Light)]
        [TestCase(OptimizationProfile.Kind.Balanced)]
        [TestCase(OptimizationProfile.Kind.Aggressive)]
        public void Detect_ReturnsProfileForItsOwnAreas(OptimizationProfile.Kind kind)
        {
            var areas = OptimizationProfile.GetAreas(kind, All);

            Assert.AreNotEqual(Enums.Memory.Areas.None, areas);
            Assert.AreEqual(kind, OptimizationProfile.Detect(areas, All));
        }

        [Test]
        public void Detect_UnmatchedOrEmptyAreas_IsCustom()
        {
            Assert.AreEqual(OptimizationProfile.Kind.Custom, OptimizationProfile.Detect(Enums.Memory.Areas.RegistryCache, All));
            Assert.AreEqual(OptimizationProfile.Kind.Custom, OptimizationProfile.Detect(Enums.Memory.Areas.None, All));
        }

        [Test]
        public void GetAreas_IsLimitedToSupportedAreas()
        {
            var supported = Enums.Memory.Areas.WorkingSet;

            Assert.AreEqual(Enums.Memory.Areas.WorkingSet, OptimizationProfile.GetAreas(OptimizationProfile.Kind.Aggressive, supported));
            // com um só tipo de área suportado os perfis coincidem; o importante é não cair em "personalizado"
            Assert.AreNotEqual(OptimizationProfile.Kind.Custom, OptimizationProfile.Detect(Enums.Memory.Areas.WorkingSet, supported));
        }

        [Test]
        public void GetAreas_Custom_IsNone()
        {
            Assert.AreEqual(Enums.Memory.Areas.None, OptimizationProfile.GetAreas(OptimizationProfile.Kind.Custom, All));
        }

        [Test]
        public void Light_UsesLowPriorityStandbyOnly()
        {
            var areas = OptimizationProfile.GetAreas(OptimizationProfile.Kind.Light, All);

            Assert.IsTrue((areas & Enums.Memory.Areas.StandbyListLowPriority) != 0);
            Assert.IsTrue((areas & Enums.Memory.Areas.StandbyList) == 0);
        }
    }
}

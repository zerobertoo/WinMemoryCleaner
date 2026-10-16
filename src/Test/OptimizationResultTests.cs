using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class OptimizationResultTests
    {
        [TestCase("chrome", "chrome", true)]
        [TestCase("CHROME", "chrome", true)]
        [TestCase("chrome", "chro", false)]
        [TestCase("chrome", "chr*", true)]
        [TestCase("msedgewebview2", "*webview?", true)]
        [TestCase("a.b", "a.b", true)]
        [TestCase("axb", "a.b", false)]
        [TestCase("", "*", false)]
        public void ProcessExclusion_MatchesExactNamesAndWildcards(string name, string entry, bool expected)
        {
            Assert.AreEqual(expected, ProcessExclusion.IsExcluded(name, new[] { entry }));
        }

        [Test]
        public void Skipped_HasNoAreasAndNoFailures()
        {
            var result = OptimizationResult.Skipped;

            Assert.IsEmpty(result.Areas);
            Assert.IsFalse(result.HasFailures);
            Assert.IsFalse(result.IsPartial);
        }

        [Test]
        public void MixedAreas_IsPartialAndListsOnlyFailed()
        {
            var result = new OptimizationResult();
            result.Areas.Add(new OptimizationAreaResult { Name = "A" });
            result.Areas.Add(new OptimizationAreaResult { Name = "B", Error = "denied" });

            Assert.IsTrue(result.HasFailures);
            Assert.IsTrue(result.IsPartial);
            Assert.That(result.Failed, Has.Exactly(1).Property("Name").EqualTo("B"));
        }

        [Test]
        public void AllFailed_IsNotPartial()
        {
            var result = new OptimizationResult();
            result.Areas.Add(new OptimizationAreaResult { Name = "A", Error = "x" });

            Assert.IsTrue(result.HasFailures);
            Assert.IsFalse(result.IsPartial);
        }
    }
}

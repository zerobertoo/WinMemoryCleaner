using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class OptimizationResultTests
    {
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

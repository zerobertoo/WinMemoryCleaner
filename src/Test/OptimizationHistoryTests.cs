using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class OptimizationHistoryTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "wmc-history-" + Guid.NewGuid().ToString("N"), "history.jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            var directory = Path.GetDirectoryName(_path);

            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        private static OptimizationHistoryEntry Entry(string reason)
        {
            return new OptimizationHistoryEntry { Timestamp = DateTimeOffset.Now, Reason = reason, ReleasedBytes = 1024, SucceededAreas = new[] { "A" } };
        }

        [Test]
        public void Read_MissingFile_ReturnsEmpty()
        {
            Assert.IsEmpty(OptimizationHistory.Read(_path));
        }

        [Test]
        public void AppendThenRead_RoundTripsInOrder()
        {
            OptimizationHistory.Append(Entry("first"), _path);
            OptimizationHistory.Append(Entry("second"), _path);

            var entries = OptimizationHistory.Read(_path);

            CollectionAssert.AreEqual(new[] { "first", "second" }, entries.Select(e => e.Reason));
            Assert.AreEqual(1024, entries[0].ReleasedBytes);
        }

        [Test]
        public void Append_KeepsOnlyMostRecentEntries()
        {
            for (var i = 0; i < 510; i++)
                OptimizationHistory.Append(Entry("r" + i), _path);

            var entries = OptimizationHistory.Read(_path);

            Assert.AreEqual(500, entries.Count);
            Assert.AreEqual("r509", entries.Last().Reason);
            Assert.AreEqual("r10", entries.First().Reason);
        }

        [Test]
        public void Read_SkipsCorruptedLines()
        {
            OptimizationHistory.Append(Entry("ok"), _path);
            File.AppendAllText(_path, "{not json" + Environment.NewLine);

            Assert.AreEqual(1, OptimizationHistory.Read(_path).Count);
        }

        [Test]
        public void FromResult_SplitsAreasAndClampsNegativeRelease()
        {
            var result = new OptimizationResult();
            result.Areas.Add(new OptimizationAreaResult { Name = "A", Elapsed = TimeSpan.FromSeconds(1) });
            result.Areas.Add(new OptimizationAreaResult { Name = "B", Elapsed = TimeSpan.FromSeconds(2), Error = "x" });

            var entry = OptimizationHistory.FromResult(result, "Manual", -500, DateTimeOffset.Now);

            CollectionAssert.AreEqual(new[] { "A" }, entry.SucceededAreas);
            CollectionAssert.AreEqual(new[] { "B" }, entry.FailedAreas);
            Assert.AreEqual(0, entry.ReleasedBytes);
            Assert.AreEqual(3, entry.DurationSeconds, 0.001);
        }
    }
}

using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class UpdaterTests
    {
        private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public void TryGetExpectedHash_FindsHashForFile()
        {
            var checksums = ValidHash + "  WinMemoryCleaner.exe\r\n" + new string('a', 64) + "  WinMemoryCleaner.zip\r\n";

            Assert.IsTrue(Updater.TryGetExpectedHash(checksums, "WinMemoryCleaner.exe", out var hash));
            Assert.AreEqual(ValidHash, hash);
        }

        [Test]
        public void TryGetExpectedHash_IsCaseInsensitiveOnFileName()
        {
            Assert.IsTrue(Updater.TryGetExpectedHash(ValidHash + "  winmemorycleaner.EXE", "WinMemoryCleaner.exe", out _));
        }

        [Test]
        public void TryGetExpectedHash_FailsWhenFileIsMissing()
        {
            Assert.IsFalse(Updater.TryGetExpectedHash(ValidHash + "  Other.exe", "WinMemoryCleaner.exe", out var hash));
            Assert.IsNull(hash);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("not-a-hash  WinMemoryCleaner.exe")]
        [TestCase("abc123  WinMemoryCleaner.exe")]
        public void TryGetExpectedHash_FailsOnMalformedInput(string checksums)
        {
            Assert.IsFalse(Updater.TryGetExpectedHash(checksums, "WinMemoryCleaner.exe", out _));
        }

        [Test]
        public void BuildReplaceCommand_RestartsServiceOnlyWhenRequested()
        {
            var restart = Updater.BuildReplaceCommand("a.exe", "t", "d", "1.0", "", "Svc", "td", true);
            var keep = Updater.BuildReplaceCommand("a.exe", "t", "d", "1.0", "", "Svc", "td", false);

            StringAssert.Contains("net stop \"Svc\"", restart);
            StringAssert.Contains("net start \"Svc\"", restart);
            StringAssert.DoesNotContain("net stop", keep);
            StringAssert.DoesNotContain("net start", keep);
            StringAssert.Contains("move /y \"t\" \"d\"", keep);
        }

        [Test]
        public void ProcessExclusionSnapshot_ReflectsAddAndRemove()
        {
            const string name = "wmc-test-process";

            try
            {
                Assert.IsTrue(Settings.AddProcessExclusion(name));
                CollectionAssert.Contains(Settings.GetProcessExclusionSnapshot(), name);
                Assert.IsTrue(Settings.RemoveProcessExclusion(name));
                CollectionAssert.DoesNotContain(Settings.GetProcessExclusionSnapshot(), name);
            }
            finally
            {
                Settings.RemoveProcessExclusion(name);
            }
        }

        [Test]
        public void BuildForwardedArguments_KeepsSimpleArguments()
        {
            Assert.AreEqual("/WorkingSet /StandbyList", Updater.BuildForwardedArguments(new[] { "/WorkingSet", "/StandbyList" }));
        }

        [Test]
        public void BuildForwardedArguments_DropsArgumentsThatCouldInjectCommands()
        {
            var result = Updater.BuildForwardedArguments(new[] { "/ok", "a&calc", "x|y", "\"quoted\"", "/a b", "%PATH%", "/ok\n", null });

            Assert.AreEqual("/ok", result);
        }

        [Test]
        public void BuildForwardedArguments_HandlesNullArray()
        {
            Assert.AreEqual(string.Empty, Updater.BuildForwardedArguments(null));
        }
    }
}

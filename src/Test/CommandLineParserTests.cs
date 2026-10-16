using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class CommandLineParserTests
    {
        private static CommandLineParser.Result Parse(params string[] args)
        {
            return CommandLineParser.Parse(CommandLineParser.Normalize(args), "1.2.3.4");
        }

        [Test]
        public void NoArguments_IsApp()
        {
            var result = Parse();

            Assert.AreEqual(Enums.StartupType.App, result.StartupType);
            Assert.AreEqual(Enums.Memory.Areas.None, result.MemoryAreas);
        }

        [Test]
        public void NullArguments_IsApp()
        {
            Assert.AreEqual(Enums.StartupType.App, CommandLineParser.Parse(null, "1.0").StartupType);
        }

        [Test]
        public void AreaArguments_AreCombinedAndSilent()
        {
            var result = Parse("/WorkingSet", "-StandbyList");

            Assert.AreEqual(Enums.StartupType.Silent, result.StartupType);
            Assert.AreEqual(Enums.Memory.Areas.WorkingSet | Enums.Memory.Areas.StandbyList, result.MemoryAreas);
        }

        [TestCase("/Install", Enums.StartupType.Installation)]
        [TestCase("-uninstall", Enums.StartupType.Uninstallation)]
        [TestCase("/RESET", Enums.StartupType.Reset)]
        [TestCase("/Service", Enums.StartupType.Service)]
        public void ModeArguments_SetStartupType(string argument, Enums.StartupType expected)
        {
            Assert.AreEqual(expected, Parse(argument).StartupType);
        }

        [Test]
        public void LastModeWins()
        {
            Assert.AreEqual(Enums.StartupType.Installation, Parse("/WorkingSet", "/Install").StartupType);
        }

        [Test]
        public void VersionArgument_FlagsUpdate()
        {
            Assert.IsTrue(Parse("1.2.3.4").UpdatedToCurrentVersion);
            Assert.IsFalse(Parse("9.9.9.9").UpdatedToCurrentVersion);
        }

        [Test]
        public void ServiceCreateArguments_QuoteExecutablePathAndKeepOrder()
        {
            var args = WinServiceInstaller.BuildCreateArguments(@"C:\Program Files\WinMemoryCleaner\app.exe");

            Assert.AreEqual("create", args[0]);
            Assert.AreEqual("\"C:\\Program Files\\WinMemoryCleaner\\app.exe\" /Service", args[3]);
            Assert.AreEqual("LocalSystem", args[^1]);
        }
    }
}

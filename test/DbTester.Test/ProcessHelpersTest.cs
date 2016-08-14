using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System.IO;
using System.Linq;
using System.Reflection;
using FakeItEasy;
using DbTester.Contracts;
using System.Diagnostics;

namespace DbTester.Test
{
    [TestClass]
    public class ProcessHelpersTest
    {
        public ILogger log;

        [TestInitialize]
        public void Initialize()
        {
            log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.ColoredConsole()
                .CreateLogger();
        }

        [TestClass]
        public class StartProcess: ProcessHelpersTest
        {
            [TestMethod]
            public void WithSqlCmd()
            {
                var processHelpers = new ProcessHelpers(log);
                processHelpers.Process = A.Fake<ProcessWrapper>();
                A.CallTo(() => processHelpers.Process.Start(null, null)).WithAnyArguments().Returns(new Process());

                var migrationInitialPath = string.Format(@"{0}\{1}", TestHelpers.GetCurrentFolder(), "Migration-Initial");
                var command = string.Format("sqlcmd -S \"(LocalDB)\\v11.0\" -d \"master\" -i \"{0}\"\\up.sql", migrationInitialPath);
                processHelpers.StartProcess(command, migrationInitialPath);

                A.CallTo(() => processHelpers.Process.Close(null)).WithAnyArguments().MustHaveHappened();
            }
        }
    }
}

using DbTester.Contracts;
using Serilog;
using System;

namespace DbTester.Contracts
{
    public interface ITester
    {
        IDatabaseHelpers DatabaseHelpers { get; set; }
        ILogger Log { get; set; }
        IProcessHelpers ProcessHelpers { get; set; }
        ISettings Settings { get; set; }

        string BuildConnectionString(string databaseName, string applicationName = null);
        string BuildDatabaseName();
        void TestCleanup(string databaseName);
        ITestResult TestInitialize();
        void ValidateSettings();
        ITestResult Test(Action<ITestResult> testAction, bool isDatabaseCleanupDisabled = false);
        void ValidateMigrationPath(string path, string pathDescription = "Settings.ScriptsFolder");
        void Migrate(string connectionString, string databaseName);
        void Seed(string connectionString, string databaseName);
        void MigrateDown(ITestResult testResult);
        string GetDownMigrationPath();
    }
}
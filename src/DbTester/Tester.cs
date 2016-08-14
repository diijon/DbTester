using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DbTester.Contracts;
using HandlebarsDotNet;
using System.IO;
using Serilog;
using System.Threading;

namespace DbTester
{
    public class Tester : Contracts.ITester
    {
        public const string upScript = "up.sql";
        public const string seedScript = "seed.sql";
        public const string downScript = "down.sql";

        public ILogger Log { get; set; }
        public ISettings Settings { get; set; }
        public IDatabaseHelpers DatabaseHelpers { get; set; }
        public IProcessHelpers ProcessHelpers { get; set; }

        public Tester(ILogger log, ISettings settings, IDatabaseHelpers databaseHelpers, IProcessHelpers processHelpers)
        {
            Log = log ?? new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.ColoredConsole()
                .CreateLogger();
            Settings = settings;
            DatabaseHelpers = databaseHelpers;
            DatabaseHelpers.Settings = DatabaseHelpers == null
                ? new DbCmdWaitTime()
                : DatabaseHelpers.Settings ?? (Settings.DbCmdWaitTime ?? new DbCmdWaitTime());
            ProcessHelpers = processHelpers;
            ProcessHelpers.Log = ProcessHelpers.Log ?? Log;
        }
        public Tester(ILogger log, ISettings settings) : this(log, settings, new DatabaseHelpers(), new ProcessHelpers(log)) { }
        public Tester(ISettings settings) : this(null, settings, new DatabaseHelpers(), new ProcessHelpers(null)) { }

        public ITestResult TestInitialize()
        {
            try
            {
                ValidateSettings();
            }
            catch(Exception ex)
            {
                throw new Exception("Failed Validation", ex);
            }

            var databaseName = BuildDatabaseName();
            var connectionString = BuildConnectionString(databaseName);

            try
            {
                Log.Information("DatabaseName: {databaseName}", databaseName);
                DatabaseHelpers.CreateDatabase(BuildConnectionString("master"), databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Creating Database", ex);
            }

            try
            {
                Migrate(connectionString, databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Migrating Database", ex);
            }

            try
            {
                Seed(connectionString, databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Seeding Database", ex);
            }

            return new TestResult
            {
                DatabaseName = databaseName,
                Tester = this,
                CompletedMigrations = new [] { Settings.MigrationPath }
            };
        }

        public void TestCleanup(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentNullException("databaseName", "Cannot equal null or empty");
            }

            try
            {
                Thread.Sleep(800);
                DatabaseHelpers.CloseDatabaseConnections(BuildConnectionString("master"), databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Closing Database Connections", ex);
            }

            try
            {
                DatabaseHelpers.DeleteDatabase(BuildConnectionString("master"), databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Deleting Database", ex);
            }
        }

        public void Migrate(string connectionString, string databaseName)
        {
            var sqlFiles = Directory.GetFiles(Settings.MigrationPath)
                .Select(x => x.Replace(Settings.MigrationPath, string.Empty))
                .Select(x => Regex.Replace(x, @"^\\", string.Empty))
                .Where(x => !(new[] { upScript, downScript, seedScript }).Contains(x))
                .Where(x => x.EndsWith(".sql"));
            sqlFiles.ToList().ForEach(file =>
            {
                var errors = DatabaseHelpers.GetSqlErrors(File.ReadAllText(string.Format(@"{0}\{1}", Settings.MigrationPath, file)));
                if (errors.Any())
                {
                    Log.Error("Migration '{MigrationPath}' Errors for File '{MigrationFile}' are {SqlErrors}", Settings.MigrationPath, file, errors);
                    throw new Exception("Sql Errors");
                }
            });

            var server = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString).DataSource;
            var sqlCmdArgs = BuildSqlCmdArgs(server, databaseName, Settings.MigrationPath, upScript);
            Log.Information("SqlCmdArgs: {sqlCmdArgs}", sqlCmdArgs);
            ProcessHelpers.StartProcess(sqlCmdArgs, Settings.MigrationPath);
        }

        public void Seed(string connectionString, string databaseName)
        {
            var files = Directory.GetFiles(Settings.MigrationPath)
                .Select(x => x.Replace(Settings.MigrationPath, string.Empty))
                .Select(x => Regex.Replace(x, @"^\\", string.Empty));
            if (!files.Any(x => x.Equals(seedScript, StringComparison.CurrentCultureIgnoreCase)))
            {
                return;
            }

            var server = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString).DataSource;
            var sqlCmdArgs = BuildSqlCmdArgs(server, databaseName, Settings.MigrationPath, seedScript);
            ProcessHelpers.StartProcess(sqlCmdArgs, Settings.MigrationPath);
        }

        public void MigrateDown(ITestResult testResult)
        {
            var downMigrations = testResult.DownMigrations ?? new IMigration[] { };
            if (!downMigrations.Any())
            {
                return;
            }

            var connectionString = BuildConnectionString("master");
            var server = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString).DataSource;
            downMigrations
                .Where(migration =>
                {
                    var files = Directory.GetFiles(migration.ScriptPath)
                        .Select(x => x.Replace(migration.ScriptPath, string.Empty))
                        .Select(x => Regex.Replace(x, @"^\\", string.Empty));
                    return files.Any(x => x.Equals(downScript, StringComparison.CurrentCultureIgnoreCase));
                })
                .Reverse()
                .ToList()
                .ForEach(migration =>
                {
                    try
                    {
                        var sqlCmdArgs = BuildSqlCmdArgs(server, migration.DatabaseName, migration.ScriptPath, downScript);
                        ProcessHelpers.StartProcess(sqlCmdArgs, migration.ScriptPath);
                    }
                    catch(Exception ex)
                    {
                        throw new Exception(string.Format("Failed Down Migration '{0}' to Database '{1}'", migration.ScriptPath, migration.DatabaseName), ex);
                    }
                });
        }

        public string GetDownMigrationPath()
        {
            var files = Directory.GetFiles(Settings.MigrationPath)
                .Select(x => x.Replace(Settings.MigrationPath, string.Empty))
                .Select(x => Regex.Replace(x, @"^\\", string.Empty));
            if (!files.Any(x => x.Equals(downScript, StringComparison.CurrentCultureIgnoreCase)))
            {
                return null;
            }

            return string.Format(@"{0}\{1}", Settings.MigrationPath, downScript);
        }

        public ITestResult Test(Action<ITestResult> testAction, bool isDatabaseCleanupDisabled = false)
        {
            var testResult = TestInitialize();
            try
            {
                testAction(testResult);
                return testResult;
            }
            finally
            {
                if (!isDatabaseCleanupDisabled)
                {
                    TestCleanup(testResult.DatabaseName);
                    MigrateDown(testResult);
                }
            }
        }

        public void ValidateSettings()
        {
            if (Settings == null)
            {
                throw new NullReferenceException("Settings");
            }
            if (string.IsNullOrEmpty(Settings.Task))
            {
                throw new FormatException("Settings.Task");
            }
            if (string.IsNullOrEmpty(Settings.TargetConnectionStringTemplate))
            {
                throw new FormatException("Settings.TargetConnectionStringTemplate");
            }
            ValidateMigrationPath(Settings.MigrationPath);
        }

        public void ValidateMigrationPath(string path, string pathDescription = "Settings.MigrationPath")
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(pathDescription, "path");
            }
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(pathDescription);
            }
            if (!Directory.GetFiles(path).Any())
            {
                throw new DirectoryNotFoundException(string.Format("{0} is an empty directory", pathDescription));
            }
            var files = Directory.GetFiles(path)
                .Select(x => x.Replace(path, string.Empty))
                .Select(x => Regex.Replace(x, @"^\\", string.Empty));
            if (!files.Any(x => x.Equals(upScript, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new FileNotFoundException(string.Format("{0} up.sql", pathDescription));
            }
        }

        public string BuildSqlCmdArgs(string server, string databaseName, string scriptPath, string sqlScript)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentNullException("databaseName", "Cannot equal null or empty");
            }
            return string.Format("sqlcmd -S \"{0}\" -I -d \"{1}\" -i \"{2}\"\\{3}", server, databaseName, scriptPath, sqlScript);
        }

        public string BuildConnectionString(string databaseName, string applicationName = null)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentNullException("databaseName", "Cannot equal null or empty");
            }

            var templateData = new
            {
                DatabaseName = databaseName,
                ApplicationName = string.IsNullOrEmpty(applicationName) ? string.Empty : string.Format("app={0};", applicationName)
            };
            var template = Handlebars.Compile(Settings.TargetConnectionStringTemplate);
            return template(templateData);
        }

        public string BuildDatabaseName()
        {
            var prefix = Settings.Task.ToUpper();

            var uniqueText = DateTime.Now.ToString("yyyyMMdd_HHmm") + String.Format("_{0:N}", Guid.NewGuid());
            if (String.IsNullOrEmpty(prefix))
            {
                return String.Format("tst__{0}", uniqueText);
            }

            if (prefix.Length > 50) //77 is actual remaining length since 128 is sql server max for db name
            {
                throw new ArgumentOutOfRangeException("prefix", "Length must be less than or equal to 50");
            }
            return String.Format("tst__{0}_{1}", prefix, uniqueText);
        }
    }

    public static class TesterInstance
    {
        public static ITestResult ThenTest(this ITestResult testResult, string migrationPath, Action<ITestResult> testAction, bool isDatabaseCleanupDisabled = false)
        {
            try
            {
                testResult.Tester.ValidateMigrationPath(migrationPath, "Migration Path");
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Validation", ex);
            }

            testResult.Tester.Settings.MigrationPath = migrationPath;
            if (testResult.DatabaseName == null)
            {
                testResult.DatabaseName = testResult.Tester.BuildDatabaseName();

                try
                {
                    testResult.Tester.Log.Information("DatabaseName: {databaseName}", testResult.DatabaseName);
                    testResult.Tester.DatabaseHelpers.CreateDatabase(testResult.Tester.BuildConnectionString("master"), testResult.DatabaseName);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed Creating Database", ex);
                }
            }
            var connectionString = testResult.Tester.BuildConnectionString(testResult.DatabaseName);

            try
            {
                testResult.Tester.Migrate(connectionString, testResult.DatabaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Migrating Database", ex);
            }

            try
            {
                testResult.Tester.Seed(connectionString, testResult.DatabaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Seeding Database", ex);
            }

            testResult.CompletedMigrations = testResult.CompletedMigrations.Concat(new[] { migrationPath });

            try
            {
                testAction(testResult);
                return testResult;
            }
            finally
            {
                if (!isDatabaseCleanupDisabled)
                {
                    testResult.Tester.TestCleanup(testResult.DatabaseName);
                    testResult.Tester.MigrateDown(testResult);
                }
            }
        }

        public static ITestResult WithDependency(this ITester tester, string migrationPath, Action<ITestResult> testAction)
        {
            var testResult = new TestResult
            {
                DatabaseName = null,
                Tester = tester,
                CompletedMigrations = new string[] { }
            };

            return testResult.WithDependency(migrationPath, testAction);
        }

        public static ITestResult MigrateDown(this ITestResult testResult, bool isDatabaseCleanupDisabled = false)
        {
            if (!isDatabaseCleanupDisabled)
            {
                testResult.Tester.TestCleanup(testResult.DatabaseName);
            }
            testResult.Tester.MigrateDown(testResult);
            return testResult;
        }

        public static ITestResult WithDependency(this ITestResult testResult, string migrationPath, Action<ITestResult> testAction)
        {
            var regx_databaseOverride = @"^Dependency\-(.*)";
            var migrationPathFolder = Path.GetFileName(migrationPath);
            try
            {
                testResult.Tester.ValidateMigrationPath(migrationPath, "Dependency Path");

                if (!Regex.IsMatch(migrationPathFolder, regx_databaseOverride))
                {
                    throw new FormatException("Dependency Folder does not identify database name");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Validation", ex);
            }

            var databaseName = Regex.Replace(migrationPathFolder, regx_databaseOverride, "$1");
            var databaseExists = testResult.Tester.DatabaseHelpers.DoesDatabaseExist(testResult.Tester.BuildConnectionString("master"), databaseName);
            if (!databaseExists)
            {
                throw new Exception(string.Format("Database '{0}' does not exist", databaseName));
            }

            testResult.Tester.Settings.MigrationPath = migrationPath;
            var connectionString = testResult.Tester.BuildConnectionString(databaseName);

            try
            {
                testResult.Tester.Migrate(connectionString, databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Migrating Database", ex);
            }

            try
            {
                testResult.Tester.Seed(connectionString, databaseName);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed Seeding Database", ex);
            }

            testResult.CompletedMigrations = testResult.CompletedMigrations.Concat(new[] { migrationPath });
            Action saveDownMigration = () =>
            {
                var downMigration = testResult.Tester.GetDownMigrationPath();
                if (string.IsNullOrEmpty(downMigration))
                {
                    return;
                }

                testResult.DownMigrations = testResult.DownMigrations.Concat(new[] { new Migration {
                    ScriptPath = migrationPath,
                    DatabaseName = databaseName
                } });
            };
            try
            {
                testAction(new TestResult //making a copy b/c i don't want the dependency db passed to subsequent tests.
                {
                    DatabaseName = databaseName,
                    Tester = testResult.Tester,
                    CompletedMigrations = testResult.CompletedMigrations
                });
                saveDownMigration();
                return testResult;
            }
            catch (Exception)
            {
                saveDownMigration();
                throw;
            }

        }
    }
}

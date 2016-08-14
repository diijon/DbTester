using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using DbTester.Contracts;
using System.IO;
using Serilog;
using System.Linq;

namespace DbTester.Test
{
    [TestClass]
    public class TesterTest
    {
        internal ILogger Log;

        [TestInitialize]
        public void Initialize()
        {
           Log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.ColoredConsole()
                .CreateLogger();
        }
        //Arrange, Act, Assert

        [TestClass]
        public class BuildConnectionString: TesterTest
        {
            public string connectionTemplate = @"Data Source=(LocalDb)\v11.0;Initial Catalog={{DatabaseName}};";

            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void WithNullDatabaseName()
            {
                var settings = A.Fake<ISettings>();
                settings.TargetConnectionStringTemplate = connectionTemplate;
                var tester = new Tester(settings);

                var connectionString = tester.BuildConnectionString(null);
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void WithEmptyDatabaseName()
            {
                var settings = A.Fake<ISettings>();
                settings.TargetConnectionStringTemplate = connectionTemplate;
                var tester = new Tester(settings);

                var connectionString = tester.BuildConnectionString(string.Empty);
            }

            [TestMethod]
            public void WithDatabaseName()
            {
                var settings = A.Fake<ISettings>();
                settings.TargetConnectionStringTemplate = connectionTemplate;
                var tester = new Tester(settings);

                var dbname = "tst__US9999_20001201_abc0123xyz";
                var connectionString = tester.BuildConnectionString(dbname);

                Assert.IsTrue(connectionString.Contains("Initial Catalog=" + dbname));
            }
        }

        [TestClass]
        public class BuildDatabaseName : TesterTest
        {
            [TestMethod]
            public void WithoutPrefix()
            {
                var settings = A.Fake<ISettings>();
                settings.Task = string.Empty;
                var tester = new Tester(settings);

                var dbname = tester.BuildDatabaseName();

                Assert.IsNotNull(dbname, "database name is null");
                Assert.IsTrue(Regex.IsMatch(dbname, @"tst__[0-9]{8}_[0-9]{4}_[0-9A-Z]{32}", RegexOptions.IgnoreCase));
            }

            [TestMethod]
            public void WithPrefix()
            {
                var settings = A.Fake<ISettings>();
                settings.Task = "US9999";
                var tester = new Tester(settings);

                var dbname = tester.BuildDatabaseName();

                Assert.IsTrue(Regex.IsMatch(dbname, @"tst__US9999_[0-9]{8}_[0-9]{4}_[0-9a-z]{32}"));
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentOutOfRangeException))]
            public void WithLongPrefix()
            {
                var settings = A.Fake<ISettings>();
                settings.Task = "US9999" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 13);
                var tester = new Tester(settings);

                Assert.AreEqual(51, settings.Task.Length);
                var dbname = tester.BuildDatabaseName();
            }
        }

        [TestClass]
        public class BuildSqlCmdArgs
        {
            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void ShouldErrorWithDatabaseArg()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);
                var server = "xx";
                var databaseName = "";
                var scriptPath = "xx";
                var sqlScriptFileName = "xx";
                var sqlCmdArgs = tester.BuildSqlCmdArgs(server, databaseName, scriptPath, sqlScriptFileName);
            }

            [TestMethod]
            public void ShouldBuild()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);
                var server = "xx";
                var databaseName = "xx";
                var scriptPath = "xx";
                var sqlScriptFileName = "xx";
                var sqlCmdArgs = tester.BuildSqlCmdArgs(server, databaseName, scriptPath, sqlScriptFileName);
                Assert.IsNotNull(sqlCmdArgs);
                Assert.IsTrue(sqlCmdArgs.Contains("-S \"xx\""));
                Assert.IsTrue(sqlCmdArgs.Contains("-d \"xx\""));
                Assert.IsTrue(sqlCmdArgs.Contains("-i \"xx\"\\xx"));
            }
        }

        [TestClass]
        public class ValidateMigrationPath : TesterTest
        {
            [TestMethod]
            public void ShouldErrorWithPathArg()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);

                var path = string.Empty;
                try
                {
                    tester.ValidateMigrationPath(path);
                    throw new Exception("Should not make it here");
                }
                catch (ArgumentException) { }


                path = PrepareExpectedFiles("ValidateMigrationPath_0");
                var file = string.Format("{0}\\{1}", path, "helloworld.txt");
                try
                {
                    tester.ValidateMigrationPath(path);
                    throw new Exception("Should not make it here");
                }
                catch(DirectoryNotFoundException ex)
                {
                    Assert.AreEqual(ex.Message, "Settings.MigrationPath");
                }


                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create directory '{Directory}'", path);
                    return;
                }
                try
                {
                    tester.ValidateMigrationPath(path);
                    throw new Exception("Should not make it here");
                }
                catch (DirectoryNotFoundException ex)
                {
                    Assert.AreEqual(ex.Message, "Settings.MigrationPath is an empty directory");
                }


                try
                {
                    File.WriteAllText(file, "hello world");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create file '{File}'", file);
                    return;
                }
                try
                {
                    tester.ValidateMigrationPath(path);
                    throw new Exception("Should not make it here");
                }
                catch (FileNotFoundException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("up.sql"));
                }

            }

            [TestMethod]
            public void ShouldPassValidation()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);

                var path = PrepareExpectedFiles("ValidateMigrationPath_1");
                var file = string.Format("{0}\\{1}", path, "up.sql");

                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create directory '{Directory}'", path);
                    return;
                }
                try
                {
                    File.WriteAllText(file, "PRINT 'hello world'");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create file '{File}'", file);
                    return;
                }

                tester.ValidateMigrationPath(path);
            }

            public void PrepareExpectedFiles()
            {
                var path = PrepareExpectedFiles("ValidateMigrationPath_2");
                var file = string.Format("{0}\\{1}", path, "helloworld.txt");
                var upSql = string.Format("{0}\\{1}", path, "up.sql");

                if (Directory.Exists(path))
                {
                    if (File.Exists(file))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Log.Error(ex, "Unauthorized access to delete file '{File}'", path);
                            return;
                        }
                    }
                    if (File.Exists(upSql))
                    {
                        try
                        {
                            File.Delete(upSql);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Log.Error(ex, "Unauthorized access to delete file '{File}'", upSql);
                            return;
                        }
                    }

                    try
                    {
                        Directory.Delete(path);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Log.Error(ex, "Unauthorized access to delete directory '{Directory}'", path);
                        return;
                    }
                }
            }
        }

        [TestClass]
        public class ValidateSettings
        {
            [TestMethod]
            public void ShouldErrorWithSettings()
            {
                ISettings settings = null;
                var tester = new Tester(settings);

                try
                {
                    tester.ValidateSettings();
                    throw new Exception("Should not make it here");
                }
                catch(NullReferenceException ex)
                {
                    Assert.AreEqual(ex.Message, "Settings");
                }
            }

            [TestMethod]
            public void ShouldErrorWithSettingsTask()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);

                try
                {
                    tester.ValidateSettings();
                    throw new Exception("Should not make it here");
                }
                catch (FormatException ex)
                {
                    Assert.AreEqual(ex.Message, "Settings.Task");
                }
            }

            [TestMethod]
            public void ShouldErrorWithSettingsTargetConnectionStringTemplate()
            {
                var settings = A.Fake<ISettings>();
                settings.Task = "xx";
                var tester = new Tester(settings);

                try
                {
                    tester.ValidateSettings();
                    throw new Exception("Should not make it here");
                }
                catch (FormatException ex)
                {
                    Assert.AreEqual(ex.Message, "Settings.TargetConnectionStringTemplate");
                }
            }
        }

        [TestClass]
        public class GetDownMigrationPath : TesterTest
        {
            [TestMethod]
            public void ShouldNotFindDownScript()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);

                var path = PrepareExpectedFiles("GetDownMigrationPath_0");
                var file = string.Format("{0}\\{1}", path, "up.sql");

                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create directory '{Directory}'", path);
                    return;
                }
                try
                {
                    File.WriteAllText(file, "PRINT 'hello world'");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create file '{File}'", file);
                    return;
                }

                settings.MigrationPath = path;
                var migrationPath = tester.GetDownMigrationPath();
                Assert.IsNull(migrationPath);
            }

            [TestMethod]
            public void ShouldFindDownScript()
            {
                var settings = A.Fake<ISettings>();
                var tester = new Tester(settings);

                var path = PrepareExpectedFiles("GetDownMigrationPath_1");
                var file = string.Format("{0}\\{1}", path, "down.sql");

                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create directory '{Directory}'", path);
                    return;
                }
                try
                {
                    File.WriteAllText(file, "PRINT 'hello world'");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Unauthorized access to create file '{File}'", file);
                    return;
                }

                settings.MigrationPath = path;
                var migrationPath = tester.GetDownMigrationPath();
                Assert.AreEqual(file, migrationPath);
            }
        }

        public string PrepareExpectedFiles(string folderSuffix)
        {
            var path = string.Format("{0}\\{1}{2}", TestHelpers.GetCurrentFolder(), "__Test_", folderSuffix);
            var testedFiles = new[] { "helloworld.txt", "up.sql", "down.sql" };

            if (!Directory.Exists(path))
            {
                return path;
            }

            testedFiles
                .Select(file => new
                {
                    FileName = file,
                    FullPath = string.Format("{0}\\{1}", path, file)
                })
                .Where(x => File.Exists(x.FullPath))
                .ToList()
                .ForEach(x =>
                {
                    try
                    {
                        File.Delete(x.FullPath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Log.Error(ex, "Unauthorized access to delete file '{File}'", x.FullPath);
                        return;
                    }
                });

            try
            {
                Directory.Delete(path);
                return path;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(ex, "Unauthorized access to delete directory '{Directory}'", path);
                return path;
            }
        }
    }
}

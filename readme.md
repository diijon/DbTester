## DbTester [![Build status](https://ci.appveyor.com/api/projects/status/vhh0lg8ll5h8umhh?svg=true)](https://ci.appveyor.com/project/diijon/dbtester)
DbTester is a helper for integration testing of SQL Server databases.
- Creates temporary databases and drops them after tests have completed
- Runs all sql through [SQLCMD](https://msdn.microsoft.com/en-us/library/ms162773.aspx)
- Parses sql for sytax errors

### Prerequisites
- Windows 7 and up
- SQLCMD
  - Its included with [Microsoft's Command Line Utilities 11 for SQL Server](https://www.microsoft.com/en-us/download/details.aspx?id=36433)

---

### Quick Start
Here I use the [Dapper ORM](https://github.com/StackExchange/dapper-dot-net) to execute all of my sql. However, any ORM or even ADO.Net will do just fine.

Folder Structure
```
VsTestExample/
+-- Simple.cs
+-- MigrationSimple-Initial               # no restrictions...for now :)
    +-- up.sql                            # required
    +-- seed.sql                          # optional
    +-- table.Words.create.sql
```

```sql
/*
 *	./MigrationSimple-Initial/up.sql
 */
:ON Error EXIT

:r table.Words.create.sql
GO
```

```sql
/*
 *	./MigrationSimple-Initial/table.Words.create.sql
 */
CREATE TABLE dbo.Words(
	Word nvarchar(256)
)
```

```sql
/*
 *	./MigrationSimple-Initial/seed.sql
 */
INSERT INTO dbo.Words VALUES('The'), ('quick'), ('brown'), ('fox'), ('jumped'), ('over'), ('Tom')
```

```c#
/*
 *	./Simple.cs
 */
[TestClass]
public class Simple
{
    internal string CurrentFolder;
    internal ITester Tester;

    [TestInitialize]
    public void Initialize()
    {
        CurrentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", string.Empty);
        Tester = new Tester(new Settings
        {
            Task = "SimpleMigrationTesting", //required, used as part of database name
            MigrationPath = string.Format(@"{0}\{1}", CurrentFolder, "MigrationSimple-Initial"), //required, up.sql file is required, seed.sql is optional
            TargetConnectionStringTemplate = @"Data Source=(LocalDb)\v11.0;Initial Catalog={{DatabaseName}};Integrated Security=SSPI;{{ApplicationName}}" //default
        });
    }

    [TestMethod]
    public void ShouldCreateWords()
    {
        Tester.Test(testResult =>
        {
            var connectionString = Tester.BuildConnectionString(testResult.DatabaseName);
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var results = conn.ExecuteScalar<int>(@"SELECT COUNT(*) FROM dbo.Words");

                Assert.AreEqual(7, results);
            }
        });
    }
}
```

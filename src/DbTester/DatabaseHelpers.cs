using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using ServiceStack;
using DbTester.Contracts;

namespace DbTester
{
    public class DatabaseHelpers : IDatabaseHelpers
    {
        public IDbCmdWaitTime Settings { get; set; }

        public DatabaseHelpers(IDbCmdWaitTime settings)
        {
            Settings = settings ?? new DbCmdWaitTime();
        }
        public DatabaseHelpers() : this(null) { }

        public void DeleteDatabase(string connectionString, string databaseName)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        SqlCmd(cmd, string.Format("DROP DATABASE [{0}]", databaseName));
                    }
                }
                catch { throw; }
                finally
                {
                    if (conn.State == ConnectionState.Open) { conn.Close(); }
                    Thread.Sleep(Settings.DeleteDatabase);
                }
            }

            var state = true;
            do
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        state = DoesDatabaseExist(connectionString, databaseName);
                    }
                    catch { throw; }
                    finally
                    {
                        if (state)
                        {
                            Console.WriteLine("Waiting on database creation of {0} to complete...", databaseName);
                            Thread.Sleep(Settings.DeleteDatabase);
                        }
                    }
                }
            }
            while (state);
        }

        public void CreateDatabase(string connectionString, string databaseName)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        SqlCmd(cmd, string.Format("CREATE DATABASE [{0}]", databaseName));
                    }
                }
                catch { throw; }
                finally
                {
                    if (conn.State == ConnectionState.Open) { conn.Close(); }
                    Thread.Sleep(Settings.CreateDatabase);
                }
            }

            byte state = 0;
            do
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = string.Format("SELECT [state] FROM sys.databases WHERE name='{0}'", databaseName);
                            state = (byte)cmd.ExecuteScalar();
                        }
                    }
                    catch { throw; }
                    finally
                    {
                        if (conn.State == ConnectionState.Open) { conn.Close(); }
                        if (state != 0)
                        {
                            Console.WriteLine("still waiting on database creation of {0} to complete...", databaseName);
                            Thread.Sleep(Settings.CreateDatabase);
                        }
                    }
                }
            }
            while (state != 0);
        }

        public bool DoesDatabaseExist(string connectionString, string databaseName)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    var sql = string.Format("SELECT [name] FROM sys.databases WHERE [name] NOT IN('master','tempdb','model','msdb') AND [name] = '{0}'", databaseName);
                    using (var reader = SqlRead(cmd, sql))
                    {
                        return reader.HasRows;
                    }
                }
            }
        }

        public void CloseDatabaseConnections(string connectionString, string databaseName)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    SqlCmd(cmd, string.Format(@"
                        DECLARE @kill varchar(8000) = '';
                        SELECT @kill = @kill + 'kill ' + CONVERT(varchar(5), spid) + ';'
                        FROM master..sysprocesses 
                        WHERE dbid = db_id('{0}')

                        IF NULLIF(LTRIM(RTRIM(@kill)),'') IS NOT NULL
                        BEGIN
	                        EXEC(@kill);
                        END
                    ", databaseName));
                }
            }
        }

        public readonly Action<SqlCommand, string> SqlCmd = (cmd, sql) =>
        {
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            cmd.ExecuteNonQuery();
        };
        public readonly Func<SqlCommand, string, SqlDataReader> SqlRead = (cmd, sql) =>
        {
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            return cmd.ExecuteReader();
        };

        public IEnumerable<string> GetSqlErrors(string sql)
        {
            var parser = new TSql120Parser(true);
            IList<ParseError> errors;
            parser.Parse(new StringReader(sql), out errors);

            return errors.Select(x => x.ToJson());

        }
    }
}

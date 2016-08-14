using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace DbTester.Contracts
{
    public interface IDatabaseHelpers
    {
        IDbCmdWaitTime Settings { get; set; }
        void CreateDatabase(string connectionString, string databaseName);
        void DeleteDatabase(string connectionString, string databaseName);
        bool DoesDatabaseExist(string connectionString, string databaseName);
        void CloseDatabaseConnections(string connectionString, string databaseName);
        IEnumerable<string> GetSqlErrors(string sql);
    }
}
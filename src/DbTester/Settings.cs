using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DbTester.Contracts;

namespace DbTester
{
    public class Settings : ISettings
    {
        public string Task { get; set; }

        private string _targetConnectionStringTemplate = @"Data Source=(LocalDb)\v11.0;Initial Catalog={{DatabaseName}};Integrated Security=SSPI;{{ApplicationName}}";
        public string TargetConnectionStringTemplate
        {
            get { return _targetConnectionStringTemplate; }
            set { _targetConnectionStringTemplate = value; }
        }

        public string MigrationPath { get; set; }

        private IDbCmdWaitTime _dbCmdWaitTime = new DbCmdWaitTime();
        public IDbCmdWaitTime DbCmdWaitTime
        {
            get { return _dbCmdWaitTime; }
            set { _dbCmdWaitTime = value ?? new DbCmdWaitTime(); }
        }
    }

    public class DbCmdWaitTime : IDbCmdWaitTime
    {
        private int _deleteDatabase = 500;
        public int DeleteDatabase
        {
            get { return _deleteDatabase; }
            set { _deleteDatabase = value; }
        }

        private int _createDatabase = 500;
        public int CreateDatabase
        {
            get { return _createDatabase; }
            set { _createDatabase = value; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DbTester.Contracts;

namespace DbTester
{
    public class TestResult : ITestResult
    {
        public string DatabaseName { get; set; }
        public ITester Tester { get; set; }
        public IEnumerable<string> CompletedMigrations { get; set; }
        public IEnumerable<IMigration> DownMigrations { get; set; }

        public TestResult()
        {
            CompletedMigrations = new List<string>();
            DownMigrations = new List<IMigration>();
        }
    }
}

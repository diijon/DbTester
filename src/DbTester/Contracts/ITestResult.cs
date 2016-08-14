using System;
using System.Collections.Generic;

namespace DbTester.Contracts
{
    public interface ITestResult
    {
        ITester Tester { get; set; }
        string DatabaseName { get; set; }
        IEnumerable<string> CompletedMigrations { get; set; }
        IEnumerable<IMigration> DownMigrations { get; set; }
    }
}
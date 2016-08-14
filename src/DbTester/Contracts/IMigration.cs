using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbTester.Contracts
{
    public interface IMigration
    {
        string ScriptPath { get; set; }
        string DatabaseName { get; set; }
    }
}

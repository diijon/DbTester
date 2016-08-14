using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbTester
{
    public class Migration: Contracts.IMigration
    {
        public string ScriptPath { get; set; }
        public string DatabaseName { get; set; }
    }
}

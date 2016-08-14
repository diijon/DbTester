using Serilog;
using System.Diagnostics;

namespace DbTester.Contracts
{
    public interface IProcessHelpers
    {
        ILogger Log { get; set; }
        ProcessWrapper Process { get; set; }
        void StartProcess(string command, string workingDirectory = null);
    }
}
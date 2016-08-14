using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Serilog;

namespace DbTester
{
    public class ProcessHelpers : Contracts.IProcessHelpers
    {
        public ILogger Log { get; set; }
        public ProcessWrapper Process { get; set; }
        public ProcessHelpers(ILogger log)
        {
            Log = log;
            Process = new ProcessWrapper();
        }

        public void StartProcess(string command, string workingDirectory = null)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = "cmd.exe";
            processInfo.Arguments = string.Format("/c {0}", command); 
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                processInfo.WorkingDirectory = workingDirectory;
            }

            var process = Process.Start(processInfo, command);
            Process.WaitForExit(process);
            Log.Information("ExitCode: {0}", Process.GetExitCode(process));
            Process.Close(process);
        }
    }

    public class ProcessWrapper
    {
        public virtual Process Start(ProcessStartInfo startInfo, string logStatement)
        {
            var process = Process.Start(startInfo);
            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Log.Information("[{Command}]>> {CommandMessage}", logStatement, e.Data);
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Log.Error("[{Command}]>> {CommandMessage}", logStatement, e.Data);
            process.BeginErrorReadLine();
            return process;
        }

        public virtual void WaitForExit(Process process)
        {
            process.WaitForExit();
        }

        public virtual void Close(Process process)
        {
            process.Close();
        }

        public virtual int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }
}

using aafccore.util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace aafccore.control
{
    /// <summary>
    /// Process starter can potentially be refactored into a thread starter
    /// Will require that all data structures and workers are checked for 
    /// data isolation and thread safety
    /// </summary>
    internal static class ProcessStarter
    {

        internal static void StartMonitorProcess(int queues)
        {
            var monitorArgs = "monitor -p monitor -w " + queues.ToString();
            StartWorkerProcess(monitorArgs);
        }

        internal static void StartNewProcessWithJob(string[] args, int jobNum, int fileRunners)
        {
            List<string> argList = new List<string> { "--batchclient", jobNum.ToString(), "--batchmode", "true" };
            StartWorkerProcess(AppendArgs(args, argList));
            if (Array.Exists(args, element => element == "localtoblob"))
            {
                // Start a file runner
                argList.Add("--fileonly");
                argList.Add("true");
                string fileRunnerArgs = AppendArgs(args, argList);
                Log.Always("Starting FILE RUNNERS");
                for (int i = 0; i < fileRunners; i++)
                {
                    StartWorkerProcess(fileRunnerArgs);
                }
            }

            if (jobNum == 0 && !Array.Exists(args, element => element == "--largefileonly"))
            {
                argList.RemoveAt(argList.Count - 1);
                argList.Remove("--fileonly");
                // Start a file runner
                argList.Add("--largefileonly");
                argList.Add("true");
                Log.Always("starting LARGE FILE RUNNER");
                StartWorkerProcess(AppendArgs(args, argList));
            }

        }

        /// <summary>
        /// Provides the template for starting worker processes in a new dos window
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static bool StartWorkerProcess(string args)
        {
            using Process workerProcess = new Process();
            workerProcess.StartInfo.UseShellExecute = true;
            workerProcess.StartInfo.FileName = Process.GetCurrentProcess().ProcessName;
            workerProcess.StartInfo.Arguments = args;
            Log.Always("ProcessStartWithArgs \": \"" +  workerProcess.StartInfo.Arguments);
            workerProcess.StartInfo.CreateNoWindow = false;
            return workerProcess.Start();
        }

        /// <summary>
        /// Creates the argument string needed to start a new process acording to the logic
        /// we need for the file copier.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="argList"></param>
        /// <returns></returns>
        private static string AppendArgs(string[] args, List<string> argList)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string arg in args)
            {
                sb.Append(" " + arg);
            }
            foreach (var newarg in argList)
            {
                sb.Append(" " + newarg);
            }
            return sb.ToString();
        }


    }
}

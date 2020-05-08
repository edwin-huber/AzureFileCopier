using aafccore.control;
using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using aafccore.work;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace aafccore
{
    /// <summary>
    /// Program is just responsible for processing the cmd line args and kicking off the workers
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            ParseArgsAndRun(args);
        }

        private static int ParseArgsAndRun(string[] args)
        {
            return Parser.Default.ParseArguments<CopyLocalToAzureFilesOptions, CopyLocalToAzureBlobOptions, MonitorOptions, ResetOptions>(args)
                              .MapResult(
                                (CopyLocalToAzureFilesOptions opts) => StartJobsOrWork(opts, args).Result,
                                (CopyLocalToAzureBlobOptions opts) => StartJobsOrWork(opts, args).Result,
                                (MonitorOptions opts) => MonitorJob(opts).Result,
                                (ResetOptions opts) => ResetCopySupportingStructures(opts).Result,
                                errs => 1);
        }

        private async static Task<int> ResetCopySupportingStructures(ResetOptions opts)
        {
            Log.QuietMode = opts.QuietMode;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Log.Always(FixedStrings.ResetModeActivatedText);
            ResetWork work = new ResetWork(opts);
            await work.Reset().ConfigureAwait(true);
            sw.Stop();
            Log.Always(FixedStrings.FinishedJob + (sw.ElapsedMilliseconds / 1000) + FixedStrings.Seconds);
            Console.ReadKey();
            return 0;
        }

        private async static Task<int> MonitorJob(MonitorOptions opts)
        {
            Monitor monitor = new Monitor(opts);
            await monitor.Start().ConfigureAwait(true);
            return 0;
        }

        private async static Task<int> StartJobsOrWork(ICopyOptions opts, string[] args)
        {

            Log.QuietMode = opts.Quiet();
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                if (opts.Batch())
                {
                    switch (opts)
                    {
                        case CopyLocalToAzureFilesOptions f:
                            IWork filework = new CopyLocalStorageToAzureFiles(f);
                            Log.Always("starting Copy Local to Azure Files worker job");
                            await filework.StartAsync().ConfigureAwait(false);
                            break;
                        case CopyLocalToAzureBlobOptions b:
                            IWork blobwork = new CopyLocalStorageToAzureBlob(b);
                            Log.Always("starting Copy Local to Azure Blob worker job");
                            await blobwork.StartAsync().ConfigureAwait(false);
                            break;
                        default:
                            throw new Exception("unknown copy type");
                    }


                }
                else
                {

                    for (int jobNum = 0; jobNum < opts.Workers(); jobNum++)
                    {
                        try
                        {
                            Log.Always("starting worker " + jobNum);
                            StartNewProcessWithJob(args, jobNum, opts.NumFileRunnersPerQueue());
                        }
                        catch (Exception e)
                        {
                            Log.Always(e.Message);
                        }
                    }
                }
                sw.Stop();
                Log.Always(FixedStrings.FinishedJob + (sw.ElapsedMilliseconds / 1000) + FixedStrings.Seconds);
            }
            catch (Exception outerCatch)
            {
                Log.Always(outerCatch.Message);
                Log.Always(outerCatch.StackTrace);
            }
            Console.ReadKey();
            return 0;
        }

        private static void StartNewProcessWithJob(string[] args, int jobNum, int fileRunners)
        {
            List<string> argList = new List<string> { "--batchclient", jobNum.ToString(), "--batchmode", "true" };
            using Process job = new Process();
            job.StartInfo.UseShellExecute = true;
            job.StartInfo.FileName = Process.GetCurrentProcess().ProcessName;
            job.StartInfo.Arguments = AppendArgs(args, argList);
            Log.Always("starting with " + job.StartInfo.Arguments);
            job.StartInfo.CreateNoWindow = false;
            job.Start();
            if (Array.Exists(args, element => element == "localtoblob"))
            {
                // Start a file runner
                argList.Add("--fileonly");
                argList.Add("true");

                using Process filerunnerJob = new Process();
                filerunnerJob.StartInfo.UseShellExecute = true;
                filerunnerJob.StartInfo.FileName = Process.GetCurrentProcess().ProcessName;
                filerunnerJob.StartInfo.Arguments = AppendArgs(args, argList);
                Log.Always("starting FILE RUNNER with " + filerunnerJob.StartInfo.Arguments);
                filerunnerJob.StartInfo.CreateNoWindow = false;

                for (int i = 0; i < fileRunners; i++)
                {
                    filerunnerJob.Start();
                }
            }

            if (jobNum == 0 && !Array.Exists(args, element => element == "--largefileonly"))
            {
                argList.RemoveAt(argList.Count - 1);
                argList.Remove("--fileonly");
                // Start a file runner
                argList.Add("--largefileonly");
                argList.Add("true");

                using Process filerunnerJob = new Process();
                filerunnerJob.StartInfo.UseShellExecute = true;
                filerunnerJob.StartInfo.FileName = Process.GetCurrentProcess().ProcessName;
                filerunnerJob.StartInfo.Arguments = AppendArgs(args, argList);
                Log.Always("starting LARGE FILE RUNNER with " + filerunnerJob.StartInfo.Arguments);
                filerunnerJob.StartInfo.CreateNoWindow = false;
                filerunnerJob.Start();
            }

        }

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

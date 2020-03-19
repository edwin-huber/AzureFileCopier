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
            return Parser.Default.ParseArguments<FolderOptions, ResetOptions>(args)
                              .MapResult(
                                (FolderOptions opts) => StartJobsOrWork(opts, args).Result,
                                (ResetOptions opts) => ResetCopySupportingStructures(opts, args).Result,
                                errs => 1);
        }

        private async static Task<int> ResetCopySupportingStructures(ResetOptions opts, string[] args)
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

        private async static Task<int> StartJobsOrWork(FolderOptions opts, string[] args)
        {
            Log.QuietMode = opts.QuietMode;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                if (opts.BatchMode)
                {
                    CopyLocalStorageToAzureFiles work = new CopyLocalStorageToAzureFiles(opts, AzureServiceFactory.ConnectToControlStorage());
                    Log.Always("starting worker job");
                    await work.Start().ConfigureAwait(false);
                }
                else
                {

                    for (int jobNum = 0; jobNum < opts.WorkerCount; jobNum++)
                    {
                        try
                        {
                            Log.Always("starting worker " + jobNum);
                            StartNewProcessWithJob(args, jobNum);
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

        private static void StartNewProcessWithJob(string[] args, int jobNum)
        {
            List<string> argList = new List<string> { "--batchclient", jobNum.ToString(), "--batchmode", "true" };

            using Process job = new Process();
            job.StartInfo.UseShellExecute = true;
            job.StartInfo.FileName = Process.GetCurrentProcess().ProcessName;
            job.StartInfo.Arguments = AppendArgs(args, argList);
            Log.Always("starting with " + job.StartInfo.Arguments);
            job.StartInfo.CreateNoWindow = false;
            job.Start();
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

using aafccore.resources;
using aafccore.util;
using aafccore.work;
using CommandLine;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace aafccore.control
{
    internal class AppStart
    {
        internal AppStart(string [] args)
        {
            ParseArgsAndRun(args);
        }
        private static int ParseArgsAndRun(string[] args)
        {
            // ToDo: Check If Async Await can be re-implemented based on the limitations of CommandLine Nuget package
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
            Monitor orchestrator = new Monitor(opts);
            await orchestrator.Start().ConfigureAwait(true);
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
                            ProcessStarter.StartNewProcessWithJob(args, jobNum, opts.NumFileRunnersPerQueue());
                        }
                        catch (Exception e)
                        {
                            Log.Always(e.Message);
                        }
                    }
                }
                StartMonitor(opts.Workers());
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

        private static void StartMonitor(int queues)
        {
            ProcessStarter.StartMonitorProcess(queues);
        }
        
    }
}

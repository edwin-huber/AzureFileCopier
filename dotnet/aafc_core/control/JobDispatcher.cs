using aafccore.resources;
using aafccore.util;
using aafccore.work;
using CommandLine;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.control
{
    internal static class JobDispatcher
    {
        private static int FolderThreadCount;
        private static int FileThreadCount;
        internal static MaxConcurrencySynchronizationContext synchronizationContext;
        internal static TaskFactory longRunningTaskFactory;
        internal static int ParseArgsAndRun(string[] args)
        {
            
            // ToDo: Check If Async Await can be re-implemented based on the limitations of CommandLine Nuget package
            return Parser.Default.ParseArguments<CopierOptions, ResetOptions>(args)
                              .MapResult(
                                (CopierOptions opts) => StartJobsOrWork(opts),
                                (ResetOptions opts) => ResetCopySupportingStructures(opts).Result,
                                errs => 1);
        }

        private async static Task<int> ResetCopySupportingStructures(ResetOptions opts)
        {
            Log.QuietMode = opts.QuietMode;
            InitSchedulers(10);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Log.Always(FixedStrings.ResetModeActivatedText);
            ResetWork work = new ResetWork(opts);
            await longRunningTaskFactory.StartNew(work.Reset, CancellationToken.None ,TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(true);
            // await work.Reset().ConfigureAwait(true);
            sw.Stop();
            Log.Always(FixedStrings.FinishedJob + (sw.ElapsedMilliseconds / 1000) + FixedStrings.Seconds);
            Console.ReadKey();
            return 0;
        }

        private static int StartJobsOrWork(CopierOptions opts)
        {
            Log.QuietMode = opts.Quiet();
            InitSchedulers(opts.WorkerCount);
            // Monitor thread
            CopierOptions monitorJobOpts = ObjectCloner.CloneJson(opts);
            monitorJobOpts.Mode = CopyMode.monitor;
            StartWorkerThread(0, monitorJobOpts, "MON", false, false);
            try
            {
                // Currently tracking folder and file thread counts separately to give us
                // flexibility to tune and adjust later
                for (int threadNum = 0; threadNum < opts.Workers(); threadNum++)
                {
                    // Folder Worker
                    StartWorkerThread(FolderThreadCount, opts, "FLD", false, false);
                    FolderThreadCount++;

                    // File Worker
                    StartWorkerThread(FileThreadCount, opts, "FIL", true, false);
                    FileThreadCount++;
                }
                // Large File Worker
                // toDo: clean this up to simplify function calls
                // maybe move the object cloning outside of the function
                StartWorkerThread(0, opts, "LRG", false, true);


            }
            catch (Exception outerCatch)
            {
                // catching generic here as we have not yet nailed down all expected exception types
                Log.Debug(outerCatch.Message, Thread.CurrentThread.Name);
                Log.Debug(outerCatch.StackTrace, Thread.CurrentThread.Name);
            }
            // Console.ReadKey(); // wait for key press which allows ending the program
            return 0;
        }

        private static void InitSchedulers(int workerCount)
        {
            // TRying Sync Context
            int MaxSync = workerCount;
            Log.Debug("Setting SynchronizationContext to max " + MaxSync.ToString(), "0");
            synchronizationContext = new MaxConcurrencySynchronizationContext(MaxSync);
            // trying scheduler
            // or LongRunning TaskCreationOptions Enum might be better
            longRunningTaskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void StartWorkerThread(int jobId, ICopyOptions opts, string name, bool fileMode, bool largeFileMode)
        {
            Log.Debug("THREAD START : " + name + jobId, Thread.CurrentThread.Name);
            CopierOptions jobOpts = opts as CopierOptions;
            jobOpts = ObjectCloner.CloneJson(jobOpts);
            jobOpts.WorkerId = jobId;
            jobOpts.FileOnlyMode = fileMode;
            jobOpts.LargeFileOnlyMode = largeFileMode;
            IWork workJob;
            switch (jobOpts.Mode)
            {
                case CopyMode.blob:
                    workJob = new CopyLocalStorageToAzureBlob(jobOpts);
                    break;
                case CopyMode.file:
                    workJob = new CopyLocalStorageToAzureFiles(jobOpts);
                    break;
                case CopyMode.monitor:
                    workJob = new QueueMonitor(10, jobOpts.WorkerCount);
                    break;
                default:
                    throw new Exception("Unknown CopyEnum Type");
            }

            //ThreadStart threadStart = new ThreadStart(workJob.StartAsync);
            //Thread jobThread = new Thread(threadStart);
            //jobThread.Name = name + jobId;
            //jobThread.Start();

#pragma warning disable CA2008 // Do not create tasks without passing a TaskScheduler
            longRunningTaskFactory.StartNew(workJob.StartAsync);
#pragma warning restore CA2008 // Do not create tasks without passing a TaskScheduler
        }
    }
}

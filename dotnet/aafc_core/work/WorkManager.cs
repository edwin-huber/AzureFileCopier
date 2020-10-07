using aafccore.control;
using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.work
{
    internal class WorkManager
    {
        private Random rnd = new Random();
        internal IWorkItemMgmt folderCopyQueue;
        internal IWorkItemMgmt fileCopyQueue;
        internal readonly IWorkItemMgmt largeFileCopyQueue;
        private readonly IWorkItemController WorkItemSubmissionController;

        protected static ISetInterface folderDoneSet;
        protected readonly double largeFileSize = Configuration.Config.GetValue<double>(ConfigStrings.LARGE_FILE_SIZE_BYTES);
        internal readonly int MaxQueueRetry = Configuration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);

        // Polly Retry Control
        protected static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);
        protected static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        protected readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);
        private CopierOptions opts;

        internal WorkManager(CopierOptions optsin)
        {
            opts = optsin;
            // Folder WorkItem mgmt needs late init, as we don't need more queues than folders!
            largeFileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.LargeFilesQueueName);
            WorkItemSubmissionController = WorkItemMgmtFactory.CreateAzureWorkItemSubmissionController(optsin.WorkerCount, optsin.WorkerId);
            folderDoneSet = AzureServiceFactory.GetFolderDoneSet();
        }

        internal void CreateWorkerQueuesForBatchProcessing(CopierOptions opts, int folderCount, int batchIndex)
        {
            if (folderCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on Worker Id
                Log.Always(FixedStrings.ProcessingQueue + opts.WorkerId);
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                Log.Always(FixedStrings.ProcessingQueue + batchIndex);
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + batchIndex);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + batchIndex);
            }
        }

        internal async Task<List<WorkItem>> GetWork(IWorkItemMgmt workQueue)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await workQueue.Fetch().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        internal async Task<bool> IsThereWork(IWorkItemMgmt workQueue)
        {
            try
            {
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    return await workQueue.WorkAvailable().ConfigureAwait(true);
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                return false;
            }
        }

        // ToDo: Refactor these 2 folder was done functions
        internal async Task<bool> WasFolderAlreadyProcessed(string path)
        { 
            return await folderDoneSet.IsMember(path).ConfigureAwait(true);
        }

        internal async Task<bool> FinishedProcessingFolder(string path)
        {
            return await folderDoneSet.Add(path).ConfigureAwait(false);
        }

        private async Task<bool> SubmitFolderWorkItem(WorkItem workitem)
        {
            return await WorkItemSubmissionController.SubmitFolder(workitem).ConfigureAwait(true);
        }

        /// <summary>
        /// Submits files to Azure Storage queues, based on file size
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        internal async Task SubmitFileWorkItems(string targetPath, List<string> files)
        {
            foreach (var file in files)
            {
                Log.Debug(FixedStrings.File + file);
                WorkItem workitem = new WorkItem() { TargetPath = targetPath, SourcePath = file };
                long length = new System.IO.FileInfo(file).Length;
                if (length > largeFileSize)
                {
                    await WorkItemSubmissionController.SubmitLargeFile(workitem).ConfigureAwait(true);
                }
                else
                {
                    await WorkItemSubmissionController.SubmitFile(workitem).ConfigureAwait(true);
                }
            }
        }

        internal void MoveWorkToNextQueue(int topLevelFoldersCount)
        {
            opts.WorkerId++;
            if (opts.WorkerId >= opts.WorkerCount)
            {
                opts.WorkerId = 0;
            }

            int batchIndex = GetBatchStartingIndex(topLevelFoldersCount, opts);
            Log.Always("BATCH INDEX " + batchIndex);
            Log.Always(FixedStrings.ProcessingQueue + batchIndex);
            if (topLevelFoldersCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on ThreadId
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + batchIndex);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + batchIndex);
            }
        }

        /// <summary>
        /// Provides the number of folders to be copied in this batch 
        /// </summary>
        /// <returns></returns>
        internal int GetBatchLength(int totalFolders, CopierOptions opts)
        {
            if (opts.WorkerCount == 1)
            {
                return totalFolders;
            }

            // we have more folders than workers...
            // some workers must do more than 1 folder
            if (opts.WorkerCount <= totalFolders)
            {
                var remainder = totalFolders % opts.WorkerCount;
                if (remainder > 0)
                {
                    // our logic works off a zero based index for batch client numbering
                    if (opts.WorkerId == opts.WorkerCount - 1)
                    {
                        // the last worker picks up any remainder
                        return (totalFolders / opts.WorkerCount) + remainder;
                    }
                }
                // this is our "batch size"
                return totalFolders / opts.WorkerCount;
            }

            // we have more workers than folders
            // we can only do a minimum of 1 folder
            return 1;
        }

        /// <summary>
        /// Provides the starting index for the range of top level folders 
        /// to be copied by this batch client
        /// </summary>
        /// <returns></returns>
        internal int GetBatchStartingIndex(int totalFolders, CopierOptions opts)
        {
            return (totalFolders / opts.WorkerCount) * opts.WorkerId;
        }

        internal async Task StartFileRunner(CopyFileFunction copyFileFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            while (true)
            {
                Log.Always(FixedStrings.StartingFileQueueLogJson + "\", \"worker\" : \"" + opts.WorkerId);
                await ProcessWorkQueue(fileCopyQueue, true, copyFileFunction, createFolderFunction, enumerateSourceFoldersFunction, enumerateSourceFilesFunction, targetAdjustmentFunction).ConfigureAwait(true);

                Log.Always("File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds");
                Thread.Sleep(Convert.ToInt32(30000 * rnd.NextDouble()));
            }
        }

        internal async Task StartLargeFileRunner(CopyFileFunction copyFileFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            while (true)
            {
                Log.Always(FixedStrings.StartingLargeFileQueueLogJson);
                await ProcessWorkQueue(largeFileCopyQueue, true, copyFileFunction, createFolderFunction, enumerateSourceFoldersFunction, enumerateSourceFilesFunction, targetAdjustmentFunction).ConfigureAwait(true);

                Log.Always("Large File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds");
                Thread.Sleep(Convert.ToInt32(30000 * rnd.NextDouble()));
            }
        }


        // using a type of strategy pattern supported by Delegates
        // Delegates used to pass in specific logic for target storage
        internal delegate bool CopyFileFunction(string sourcePath, string targetPath);
        internal delegate bool CreateFolderFunction(string targetPath);
        internal delegate List<string> EnumerateSourcesFunction(string sourcePath);
        internal delegate string TargetAdjustmentFunction(string target, CopierOptions opts);

        /// <summary>
        /// Processes work items from the Azure storage queues.
        /// Based on current logic, we have 3 queues per job:
        /// - folder
        /// - file
        /// - largefile
        /// Only folder queues are differentiated based on the job / batch client number
        /// </summary>
        /// <param name="workQueue"></param>
        /// <param name="isFileQueue"></param>
        /// <param name="copyFunction"></param>
        /// <param name="createFolderFunction"></param>
        /// <returns></returns>
        internal async Task ProcessWorkQueue(IWorkItemMgmt workQueue, bool isFileQueue, CopyFileFunction copyFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            int retryCount = 0;
            try
            {
                // we loop through several times, in case there are other workers still submitting stuff...
                while (retryCount < MaxQueueRetry)
                {
                    bool thereIsWork = await IsThereWork(workQueue).ConfigureAwait(false);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        List<WorkItem> workitems = await GetWork(workQueue).ConfigureAwait(false);

                        foreach (var workitem in workitems)
                        {
                            if (workitem != null && workitem.Empty == false)
                            {
                                if (isFileQueue)
                                {
                                    // call the copy delegate
                                    if (copyFunction(workitem.SourcePath, workitem.TargetPath))
                                    {
                                        workitem.Succeeded = true;
                                    }
                                }
                                else
                                {
                                    // we do not create folders in blob storage, the folder names serve as file name prefix...
                                    if (await WasFolderAlreadyProcessed(workitem.SourcePath).ConfigureAwait(false) == false)
                                    {
                                        Log.Always(FixedStrings.CreatingDirectory + workitem.TargetPath);
                                        if (!createFolderFunction(workitem.TargetPath))
                                        {
                                            Log.Always(ErrorStrings.FailedCopy + workitem.TargetPath);
                                        }
                                        await SubmitFolderWorkitems(enumerateSourceFoldersFunction(workitem.SourcePath), opts, targetAdjustmentFunction).ConfigureAwait(true);
                                        await SubmitFileWorkItems(workitem.TargetPath, enumerateSourceFilesFunction(workitem.SourcePath)).ConfigureAwait(true);
                                    }

                                    // Folder was done or already done
                                    // We don't want this message hanging around the queue... as they are annoying the sysadmin...
                                    await FinishedProcessingFolder(workitem.SourcePath).ConfigureAwait(false);
                                    workitem.Succeeded = true;
                                }
                            }
                        }
                        await workQueue.CompleteWork().ConfigureAwait(true);
                    }
                    else
                    {
                        if (!isFileQueue)
                        {
                            // only folder queues should run out of work to do
                            // file queues might need to sleep for work to appear
                            retryCount++;
                            Thread.Sleep(60000); // Folder queues sleep 60 seconds in case failed objects need to reappear...
                        }
                        // jittering the retry
                        Log.Always("Unable to find work, retrying in a moment... if all queues are empty, press any key to exit");
                        Random rnd = new Random();
                        int sleepTime = rnd.Next(1, 3) * 10000;
                        Thread.Sleep(sleepTime);
                    }
                }
            }
            catch (Exception cf)
            {
                Log.Always(ErrorStrings.ErrorProcessingWorkException);
                Log.Always(cf.Message);
                Log.Always(cf.StackTrace);
                Log.Always(cf.InnerException.Message);
                Log.Always(cf.InnerException.StackTrace);
                return;
            }
            Log.Always(FixedStrings.RanOutOfQueueMessages);
        }

        /// <summary>
        /// Submits folder work items to the azure storage queue
        /// </summary>
        /// <param name="folders"></param>
        /// <returns></returns>
        internal async Task SubmitFolderWorkitems(List<string> folders, CopierOptions opts, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            foreach (var folder in folders)
            {
                if (opts.FullCheck || (!await WasFolderAlreadyProcessed(folder).ConfigureAwait(false)))
                {
                    // ToDo: Refactoring - right now I am leaving this here, as I didn't want to have the System IO references
                    // to manipulate the directory paths in the WorkManager class
                    WorkItem workitem = new WorkItem() { TargetPath = targetAdjustmentFunction(folder, opts), SourcePath = folder };
                    await SubmitFolderWorkItem(workitem).ConfigureAwait(false);
                }
            }
        }


    }
}

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
        protected readonly double largeFileSize = CopierConfiguration.Config.GetValue<double>(ConfigStrings.LARGE_FILE_SIZE_BYTES);
        internal readonly int MaxQueueRetry = CopierConfiguration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);

        // Polly Retry Control
        protected static readonly int maxRetryAttempts = CopierConfiguration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);
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
                Log.Debug(FixedStrings.ProcessingQueue + opts.WorkerId, Thread.CurrentThread.Name);
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                Log.Debug(FixedStrings.ProcessingQueue + batchIndex, Thread.CurrentThread.Name);
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + batchIndex);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + batchIndex);
            }
        }

        internal List<WorkItem> GetWork(IWorkItemMgmt workQueue)
        {
                return workQueue.Fetch();
        }

        internal bool IsThereWork(IWorkItemMgmt workQueue)
        {
            try
            {
                // ToDo: IMplement synchronous retry loop to replace polly
                return workQueue.WorkAvailable();
            }
            catch (Exception e)
            {
                Log.Debug(e.Message, Thread.CurrentThread.Name);
                return false;
            }
        }

        // ToDo: Refactor these 2 folder was done functions
        internal bool WasFolderAlreadyProcessed(string path)
        {
            return folderDoneSet.IsMember(path).Result;
        }

        internal bool FinishedProcessingFolder(string path)
        {
            return folderDoneSet.Add(path).Result;
        }

        private bool SubmitFolderWorkItem(WorkItem workitem)
        {
            return  WorkItemSubmissionController.SubmitFolder(workitem);
        }

        /// <summary>
        /// Submits files to Azure Storage queues, based on file size
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        internal void SubmitFileWorkItems(string targetPath, List<string> files)
        {
            foreach (var file in files)
            {
                Log.Debug(FixedStrings.File + file, Thread.CurrentThread.Name);
                WorkItem workitem = new WorkItem() { TargetPath = targetPath, SourcePath = file };
                long length = new System.IO.FileInfo(file).Length;
                if (length > largeFileSize)
                {
                    WorkItemSubmissionController.SubmitLargeFile(workitem);
                }
                else
                {
                    WorkItemSubmissionController.SubmitFile(workitem);
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
            Log.Debug("BATCH INDEX " + batchIndex, Thread.CurrentThread.Name);
            Log.Debug(FixedStrings.ProcessingQueue + batchIndex, Thread.CurrentThread.Name);
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
            return (totalFolders * opts.WorkerId) / opts.WorkerCount;
        }

        internal void StartFileRunner(CopyFileFunction copyFileFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            while (true)
            {
                Log.Debug(FixedStrings.StartingFileQueueLogJson + "\", \"worker\" : \"" + opts.WorkerId, Thread.CurrentThread.Name);
                ProcessWorkQueue(fileCopyQueue, true, copyFileFunction, createFolderFunction, enumerateSourceFoldersFunction, enumerateSourceFilesFunction, targetAdjustmentFunction);

                Log.Debug("File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds", Thread.CurrentThread.Name);
                Thread.Sleep(Convert.ToInt32(30000 * rnd.NextDouble()));
            }
        }

        internal void StartLargeFileRunner(CopyFileFunction copyFileFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            while (true)
            {
                Log.Debug(FixedStrings.StartingLargeFileQueueLogJson, Thread.CurrentThread.Name);
                ProcessWorkQueue(largeFileCopyQueue, true, copyFileFunction, createFolderFunction, enumerateSourceFoldersFunction, enumerateSourceFilesFunction, targetAdjustmentFunction);

                Log.Debug("Large File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds", Thread.CurrentThread.Name);
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
        internal void ProcessWorkQueue(IWorkItemMgmt workQueue, bool isFileQueue, CopyFileFunction copyFunction, CreateFolderFunction createFolderFunction, EnumerateSourcesFunction enumerateSourceFoldersFunction, EnumerateSourcesFunction enumerateSourceFilesFunction, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            int retryCount = 0;
            try
            {
                // we loop through several times, in case there are other workers still submitting stuff...
                while (retryCount < MaxQueueRetry)
                {
                    bool thereIsWork = IsThereWork(workQueue);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        List<WorkItem> workitems = GetWork(workQueue);

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
                                        Log.IncrementFileCounter();
                                    }
                                }
                                else
                                {
                                    // we do not create folders in blob storage, the folder names serve as file name prefix...
                                    if (WasFolderAlreadyProcessed(workitem.SourcePath) == false)
                                    {
                                        Log.Debug(FixedStrings.CreatingDirectory + workitem.TargetPath, Thread.CurrentThread.Name);
                                        if (!createFolderFunction(workitem.TargetPath))
                                        {
                                            Log.Always(ErrorStrings.FailedCopy + workitem.TargetPath);
                                        }
                                        Log.IncrementFolderCounter();
                                        SubmitFolderWorkitems(enumerateSourceFoldersFunction(workitem.SourcePath), opts, targetAdjustmentFunction);
                                        SubmitFileWorkItems(workitem.TargetPath, enumerateSourceFilesFunction(workitem.SourcePath));
                                    }

                                    // Folder was done or already done
                                    // We don't want this message hanging around the queue... as they are annoying the sysadmin...
                                    FinishedProcessingFolder(workitem.SourcePath);
                                    workitem.Succeeded = true;
                                }
                            }
                        }
                        workQueue.CompleteWork();
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
                        Log.Debug("Unable to find work, retrying in a moment...", Thread.CurrentThread.ManagedThreadId.ToString());
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
        internal void SubmitFolderWorkitems(List<string> folders, CopierOptions opts, TargetAdjustmentFunction targetAdjustmentFunction)
        {
            foreach (var folder in folders)
            {
                if (opts.FullCheck || WasFolderAlreadyProcessed(folder) == false)
                {
                    // ToDo: Refactoring - right now I am leaving this here, as I didn't want to have the System IO references
                    // to manipulate the directory paths in the WorkManager class
                    WorkItem workitem = new WorkItem() { TargetPath = targetAdjustmentFunction(folder, opts), SourcePath = folder };
                    SubmitFolderWorkItem(workitem);
                }
            }
        }


    }
}

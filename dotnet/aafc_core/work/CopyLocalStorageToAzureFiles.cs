using aafccore.control;
using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.storagemodel;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.work
{
    internal class CopyLocalStorageToAzureFiles
    {
        private AzureQueueWorkItemMgmt azureFolderWorkItemMgmt;
        private readonly CloudStorageAccount cloudStorageAccount;
        private AzureQueueWorkItemMgmt azureFileWorkItemMgmt;
        private readonly AzureQueueWorkItemMgmt azureLargeFileWorkItemMgmt;
        private readonly AzureFilesTargetStorage azureFilesTargetStorage;
        private readonly LocalFileStorage localFileStorage;
        private static ISetInterface folderDoneSet;
        private readonly FolderOptions opts;
        private readonly StringBuilder pathAdjuster = new StringBuilder(300);
        private readonly double largeFileSize = Configuration.Config.GetValue<double>(ConfigStrings.LARGE_FILE_SIZE_BYTES);

        // Polly Retry Control
        private static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.MAX_RETRY);
        private static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        private readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

        private readonly int originalWorkerId;
        private int batchLength;
        private int topLevelFoldersCount;
        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal CopyLocalStorageToAzureFiles(FolderOptions optsin, CloudStorageAccount cloudStorageAccountIn)
        {
            opts = optsin;
            originalWorkerId = opts.WorkerId;
            // Folder WorkItem mgmt needs late init, as we don't need more queues than folders!
            cloudStorageAccount = cloudStorageAccountIn;
            azureLargeFileWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.LargeFilesQueueName, true);
            azureFilesTargetStorage = new AzureFilesTargetStorage();
            localFileStorage = new LocalFileStorage(opts.ExcludeFolders.Split(',').ToList<string>(),opts.ExcludeFiles.Split(",").ToList<string>());
            folderDoneSet = AzureServiceFactory.GetFolderDoneSet();
        }

        /// <summary>
        /// Starts the processing of work given the path provided to the tool
        /// If there are already messages in the folder queue, those will be processed first...
        /// </summary>
        /// <returns></returns>
        internal async Task Start()
        {
            // first enumerate top level and add to queue.
            // Need t
            SubmitBatchedTopLevelWorkitems();
            await ProcessAllWork().ConfigureAwait(false);

            // now go through other queues 
            if (opts.WorkerCount > 1)
            {
                MoveWorkToNextQueue();
                while (opts.WorkerId != originalWorkerId)
                {
                    await ProcessAllWork().ConfigureAwait(false);
                    MoveWorkToNextQueue();
                }
            }
        }

        private void MoveWorkToNextQueue()
        {
            opts.WorkerId++;
            if(opts.WorkerId >= opts.WorkerCount)
            {
                opts.WorkerId = 0;
            }

            int batchIndex = GetBatchStartingIndex(topLevelFoldersCount);
            Log.Always("BATCH INDEX " + batchIndex);
            Log.Always(FixedStrings.ProcessingQueue + batchIndex);
            if (topLevelFoldersCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on ThreadId
                azureFolderWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId, false);
                azureFileWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId, false);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                azureFolderWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + batchIndex, false);
                azureFileWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + batchIndex, false);
            }
        }

        private async Task ProcessAllWork()
        {
            // requires that we can connect to the folder work queue
            // Process folders queue until done
            Log.Always("Processing Folder Work Queue");
            await ProcessWorkQueue(azureFolderWorkItemMgmt, false).ConfigureAwait(true);
            Log.Always("Processing File Work Queue");
            // process files queue until done
            await ProcessWorkQueue(azureFileWorkItemMgmt, true).ConfigureAwait(true);
            Log.Always("Processing LargeFile Work Queue");
            // process large files queue until done
            await ProcessWorkQueue(azureLargeFileWorkItemMgmt, true).ConfigureAwait(true);
        }

        /// <summary>
        /// Needed when starting the job to only submit folders which will be processed 
        /// by the associated folder queue
        /// </summary>
        private async void SubmitBatchedTopLevelWorkitems()
        {
            var topLevelFolders = localFileStorage.EnumerateFolders(opts.Path);
            topLevelFoldersCount = topLevelFolders.Count;
            topLevelFolders.Sort();
            
            batchLength = GetBatchLength(topLevelFoldersCount);
            int batchIndex = GetBatchStartingIndex(topLevelFoldersCount);

            topLevelFolders = topLevelFolders.GetRange(batchIndex, batchLength);

            Log.Always(FixedStrings.ProcessingQueue + batchIndex);
            if(topLevelFoldersCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on ThreadId
                azureFolderWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId, false);
                azureFileWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId, false);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                azureFolderWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + batchIndex, false);
                azureFileWorkItemMgmt = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + batchIndex, false);
            }

            foreach (var folder in topLevelFolders)
            {
                WorkItem workitem = new WorkItem() { TargetPath = AdjustTargetFolderPath(folder), SourcePath = folder };
                await azureFolderWorkItemMgmt.Submit(workitem).ConfigureAwait(true);
            }

            // we only want to copy the root files once
            if (opts.WorkerId == 0)
            {
                await SubmitFileWorkItems(AdjustTargetFolderPath(opts.Path), localFileStorage.EnumerateFiles(opts.Path)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Provides the starting index for the range of top level folders 
        /// to be copied by this batch client
        /// </summary>
        /// <returns></returns>
        private int GetBatchStartingIndex(int totalFolders)
        {
            return (totalFolders / opts.WorkerCount) * opts.WorkerId;
        }

        /// <summary>
        /// Provides the number of folders to be copied in this batch 
        /// </summary>
        /// <returns></returns>
        private int GetBatchLength(int totalFolders)
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
        /// Processes work items from the Azure storage queues.
        /// Based on current logic, we have 3 queues per job:
        /// - folder
        /// - file
        /// - largefile
        /// Only folder queues are differentiated based on the job / batch client number
        /// </summary>
        /// <param name="workQueue"></param>
        /// <param name="isFileQueue"></param>
        /// <returns></returns>
        private async Task ProcessWorkQueue(AzureQueueWorkItemMgmt workQueue, bool isFileQueue)
        {
            int retryCount = 0;
            try
            {
                // we loop through 3 times, in case there are other workers still submitting stuff...
                while (retryCount < 3)
                {
                    bool thereIsWork = await IsThereWork(workQueue).ConfigureAwait(false);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        WorkItem workitem = await GetWork(workQueue).ConfigureAwait(false);

                        if (workitem != null && workitem.Empty == false)
                        {
                            if (isFileQueue)
                            {
                                azureFilesTargetStorage.CopyFile(workitem.SourcePath, workitem.TargetPath);
                            }
                            else
                            {
                                if (await FolderWasNotAlreadyCompleted(workitem).ConfigureAwait(false))
                                {
                                    Log.Always(FixedStrings.CreatingDirectory + workitem.TargetPath);
                                    if (!azureFilesTargetStorage.CreateFolder(workitem.TargetPath))
                                    {
                                        Log.Always(ErrorStrings.FailedCopy + workitem.TargetPath);
                                    }
                                    await SubmitFolderWorkitems(localFileStorage.EnumerateFolders(workitem.SourcePath)).ConfigureAwait(true);
                                    await SubmitFileWorkItems(workitem.TargetPath, localFileStorage.EnumerateFiles(workitem.SourcePath)).ConfigureAwait(true);
                                    await folderDoneSet.Add(workitem.SourcePath).ConfigureAwait(false);
                                }
                            }
                            await workQueue.CompleteWork().ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        retryCount++;
                        // jittering the retry
                        Random rnd = new Random();
                        int sleepTime = rnd.Next(1, 3) * 250;
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

        private async Task<bool> FolderWasNotAlreadyCompleted(WorkItem workItem)
        {
            var done = await folderDoneSet.IsMember(workItem.SourcePath).ConfigureAwait(true);
            return !done;
        }

        private async Task<WorkItem> GetWork(AzureQueueWorkItemMgmt workQueue)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await workQueue.Fetch().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task<bool> IsThereWork(AzureQueueWorkItemMgmt workQueue)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await workQueue.WorkAvailable().ConfigureAwait(true);
            }).ConfigureAwait(false);
        }



        /// <summary>
        /// Submits folder work items to the azure storage queue
        /// </summary>
        /// <param name="folders"></param>
        /// <returns></returns>
        private async Task SubmitFolderWorkitems(List<string> folders)
        {
            foreach (var folder in folders)
            {
                if (opts.FullCheck || (!opts.FullCheck && !await folderDoneSet.IsMember(folder).ConfigureAwait(false)))
                {
                    WorkItem workitem = new WorkItem() { TargetPath = AdjustTargetFolderPath(folder), SourcePath = folder };
                    await azureFolderWorkItemMgmt.Submit(workitem).ConfigureAwait(true);
                }
            }
        }

        /// <summary>
        /// Adjusts the target folder path to the correct form, removing and inserting as necessary
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private string AdjustTargetFolderPath(string folder)
        {
            pathAdjuster.Clear();
            if (!string.IsNullOrEmpty(opts.DestinationSubFolder))
            {
                pathAdjuster.Append(opts.DestinationSubFolder);
            }

            if (folder.Length < 1)
            {
                // no adjust
                Log.Always("Copying root folder");
            }
            else
            {
                if (folder[Directory.GetDirectoryRoot(folder).Length..].StartsWith(opts.PathToRemove, StringComparison.InvariantCulture))
                {
                    pathAdjuster.Append(folder[(Directory.GetDirectoryRoot(folder).Length + opts.PathToRemove.Length)..]);
                }
                else
                {
                    pathAdjuster.Append(folder[Directory.GetDirectoryRoot(folder).Length..]);
                }
                pathAdjuster.Replace('\\', '/');
            }

            return pathAdjuster.ToString().TrimStart('/');
        }

        /// <summary>
        /// Submits files to Azure Storage queues, based on file size
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        private async Task SubmitFileWorkItems(string targetPath, List<string> files)
        {
            foreach (var file in files)
            {
                Log.Debug(FixedStrings.File + file);
                WorkItem workitem = new WorkItem() { TargetPath = targetPath, SourcePath = file };
                long length = new System.IO.FileInfo(file).Length;
                if (length > largeFileSize)
                {
                    await azureLargeFileWorkItemMgmt.Submit(workitem).ConfigureAwait(true);
                }
                else
                {
                    await azureFileWorkItemMgmt.Submit(workitem).ConfigureAwait(true);
                }
            }
        }
    }
}

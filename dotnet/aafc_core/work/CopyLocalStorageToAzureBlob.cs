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
    internal class CopyLocalStorageToAzureBlob : LocalFileSystemSourceCopy
    {
        private readonly AzureBlobTargetStorage azureBlobTargetStorage;

        private readonly CopyLocalToAzureBlobOptions opts;

        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal CopyLocalStorageToAzureBlob(CloudStorageAccount cloudStorageAccountIn, CopyLocalToAzureBlobOptions optsin) : base(cloudStorageAccountIn, optsin)
        {
            opts = optsin;
            azureBlobTargetStorage = new AzureBlobTargetStorage();
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
            SubmitBatchedTopLevelWorkitems(opts);
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

            int batchIndex = GetBatchStartingIndex(topLevelFoldersCount, opts);
            Log.Always("BATCH INDEX " + batchIndex);
            Log.Always(FixedStrings.ProcessingQueue + batchIndex);
            if (topLevelFoldersCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on ThreadId
                folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId, false);
                fileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId, false);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + batchIndex, false);
                fileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + batchIndex, false);
            }
        }

        /// <summary>
        /// Processes work based on workitems found in the queues
        /// </summary>
        /// <returns></returns>
        private async Task ProcessAllWork()
        {
            // ToDo: Add Job / Queue Id to log events
            Log.Always(FixedStrings.StartingFolderQueueLogJson);
            await ProcessWorkQueue(folderCopyQueue, false).ConfigureAwait(true);
            
            Log.Always(FixedStrings.StartingFileQueueLogJson);
            await ProcessWorkQueue(fileCopyQueue, true).ConfigureAwait(true);

            Log.Always(FixedStrings.StartingLargeFileQueueLogJson);
            await ProcessWorkQueue(largeFileCopyQueue, true).ConfigureAwait(true);
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
                // we loop through several times, in case there are other workers still submitting stuff...
                while (retryCount < MaxQueueRetry)
                {
                    bool thereIsWork = await IsThereWork(workQueue).ConfigureAwait(false);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        List <WorkItem> workitems = await GetWork(workQueue).ConfigureAwait(false);

                        foreach (var workitem in workitems)
                        {
                            if (workitem != null && workitem.Empty == false)
                            {
                                if (isFileQueue)
                                {
                                    if(azureBlobTargetStorage.CopyFile(workitem.SourcePath, workitem.TargetPath))
                                    {
                                        workitem.Succeeded = true;
                                    }
                                }
                                else
                                {
                                    // we do not create folders in blob storage, the folder names serve as file name prefix...
                                    if (await FolderWasNotAlreadyCompleted(workitem).ConfigureAwait(false))
                                    {
                                        Log.Always(FixedStrings.CreatingDirectory + workitem.TargetPath);
                                        await SubmitFolderWorkitems(localFileStorage.EnumerateFolders(workitem.SourcePath), opts).ConfigureAwait(true);
                                        await SubmitFileWorkItems(workitem.TargetPath, localFileStorage.EnumerateFiles(workitem.SourcePath)).ConfigureAwait(true);
                                        await folderDoneSet.Add(workitem.SourcePath).ConfigureAwait(false);
                                        workitem.Succeeded = true;
                                    }
                                }
                            }
                        }
                        await workQueue.CompleteWork().ConfigureAwait(true);
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
    }
}

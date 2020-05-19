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
    /// <summary>
    /// The Azure Blob Target copy logic is a little different to the copy to Azure files, 
    /// as Azure blobs have a flat structure, and only through the use of prefixes do we "simulate" folder structures
    /// The creation of folders does not occur.
    /// Especially for deep and complex folder structures, we found that the file copy process can take a long time
    /// As such, the blob copy will use a "file queue runner concept", which will copy files only, whilst the standard
    /// runners will loop through all queues.
    /// </summary>
    internal class CopyLocalStorageToAzureBlob : LocalFileSystemSourceCopy, IWork
    {
        private readonly AzureBlobTargetStorage azureBlobTargetStorage;
        private Random rnd = new Random();
        private readonly CopyLocalToAzureBlobOptions opts;

        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal CopyLocalStorageToAzureBlob(CopyLocalToAzureBlobOptions optsin) : base(optsin)
        {
            opts = optsin;
            azureBlobTargetStorage = new AzureBlobTargetStorage();
        }

        /// <summary>
        /// Starts the processing of work given the path provided to the tool
        /// If there are already messages in the folder queue, those will be processed first...
        /// </summary>
        /// <returns></returns>
        async Task IWork.StartAsync()
        {
            // first enumerate top level and add to queue.

            if (opts.FileOnlyMode)
            {
                Log.Always("FILE_RUNNER_START");
                folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId);
                fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId);
                await StartFileRunner().ConfigureAwait(false);
            }
            else if (opts.LargeFileOnlyMode)
            {
                Log.Always("LARGE_FILE_RUNNER_START");
                await StartLargeFileRunner().ConfigureAwait(false);
            }
            else
            {
                if (!opts.Resume)
                {
                    SubmitBatchedTopLevelWorkitems(opts);
                }
                // ToDo: Add Job / Queue Id to log events
                Log.Always(FixedStrings.StartingFolderQueueLogJson + "\":\"" + opts.WorkerId);
                await ProcessWorkQueue(folderCopyQueue, false).ConfigureAwait(true);

            }
        }

        private async Task StartFileRunner()
        {
            while (true)
            {
                Log.Always(FixedStrings.StartingFileQueueLogJson + "\", \"worker\" : \"" + opts.WorkerId);
                await ProcessWorkQueue(fileCopyQueue, true).ConfigureAwait(true);

                Log.Always("File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds");
                Thread.Sleep(Convert.ToInt32(30000 * rnd.NextDouble()));
            }
        }

        private async Task StartLargeFileRunner()
        {
            while (true)
            {
                Log.Always(FixedStrings.StartingLargeFileQueueLogJson);
                await ProcessWorkQueue(largeFileCopyQueue, true).ConfigureAwait(true);

                Log.Always("Large File runner " + opts.WorkerId + ", starting new loop in under 30 Seconds");
                Thread.Sleep(Convert.ToInt32(30000 * rnd.NextDouble()));
            }
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
        private async Task ProcessWorkQueue(IWorkItemMgmt workQueue, bool isFileQueue)
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
                                    }

                                    // Folder was done or already done
                                    // We don't want this message hanging around the queue... as they are annoying the sysadmin...
                                    await folderDoneSet.Add(workitem.SourcePath).ConfigureAwait(false);
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
                        Log.Always("Unable to find work, retrying in a moment...");
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
    }
}

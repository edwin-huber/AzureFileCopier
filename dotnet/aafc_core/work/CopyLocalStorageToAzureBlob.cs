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
using System.Reflection.Metadata.Ecma335;
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

        private readonly CopierOptions opts;

        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal CopyLocalStorageToAzureBlob(CopierOptions optsin) : base(optsin)
        {
            opts = optsin;
            azureBlobTargetStorage = new AzureBlobTargetStorage();
        }

        /// <summary>
        /// Starts the processing of work given the path provided to the tool
        /// If there are already messages in the folder queue, those will be processed first...
        /// </summary>
        /// <returns></returns>
        async void IWork.StartAsync()
        {
            // first enumerate top level and add to queue.

            if (opts.FileOnlyMode)
            {
                Log.Debug("FILE_RUNNER_START", Thread.CurrentThread.Name);
                
                base.workManager.fileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId);
                base.workManager.StartFileRunner(azureBlobTargetStorage.CopyFile, BlobCreateFolderStub, localFileStorage.EnumerateFolders, localFileStorage.EnumerateFiles, base.AdjustTargetFolderPath);
                // await Task.Factory.StartNew(base.workManager.StartFileRunner, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(true);
            }
            else if (opts.LargeFileOnlyMode)
            {
                Log.Debug("LARGE_FILE_RUNNER_START", Thread.CurrentThread.Name);
                base.workManager.StartLargeFileRunner(azureBlobTargetStorage.CopyFile, BlobCreateFolderStub, localFileStorage.EnumerateFolders, localFileStorage.EnumerateFiles, base.AdjustTargetFolderPath);
            }
            else
            {
                base.workManager.folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId);
                if (!opts.Resume)
                {
                    PrepareBatchedProcessingAndQueues(opts);
                }
                // ToDo: Add Job / Queue Id to log events
                Log.Debug(FixedStrings.StartingFolderQueueLogJson + "\":\"" + opts.WorkerId, Thread.CurrentThread.Name);
                base.workManager.ProcessWorkQueue(base.workManager.folderCopyQueue, false, azureBlobTargetStorage.CopyFile, BlobCreateFolderStub, localFileStorage.EnumerateFolders,localFileStorage.EnumerateFiles, base.AdjustTargetFolderPath);

            }
        }

        // No folders in Blob storage
        public static bool BlobCreateFolderStub(string path)
        {
            return true;
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
        private void ProcessWorkQueue(IWorkItemMgmt workQueue, bool isFileQueue)
        {
            int retryCount = 0;
            try
            {
                // we loop through several times, in case there are other workers still submitting stuff...
                while (retryCount < base.workManager.MaxQueueRetry)
                {
                    bool thereIsWork = base.workManager.IsThereWork(workQueue);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        List <WorkItem> workitems = base.workManager.GetWork(workQueue);

                        foreach (var workitem in workitems)
                        {
                            if (workitem != null && workitem.Empty == false)
                            {
                                if (isFileQueue)
                                {
                                    if(azureBlobTargetStorage.CopyFile(workitem.SourcePath, workitem.TargetPath))
                                    {
                                        workitem.Succeeded = true;
                                        Log.IncrementFileCounter();
                                    }
                                }
                                else
                                {
                                    // we do not create folders in blob storage, the folder names serve as file name prefix...
                                    if (base.workManager.WasFolderAlreadyProcessed(workitem.SourcePath) == false)
                                    {
                                        Log.Debug(FixedStrings.CreatingDirectory + workitem.TargetPath, Thread.CurrentThread.Name);
                                        base.workManager.SubmitFolderWorkitems(localFileStorage.EnumerateFolders(workitem.SourcePath), opts, base.AdjustTargetFolderPath);
                                        base.workManager.SubmitFileWorkItems(workitem.TargetPath, localFileStorage.EnumerateFiles(workitem.SourcePath));
                                    }

                                    // Folder was done or already done
                                    // We don't want this message hanging around the queue... as they are annoying the sysadmin...
                                    base.workManager.FinishedProcessingFolder(workitem.SourcePath);
                                    Log.IncrementFolderCounter();
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
                        Log.Debug("Unable to find work, retrying in a moment... ", Thread.CurrentThread.ManagedThreadId.ToString());
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

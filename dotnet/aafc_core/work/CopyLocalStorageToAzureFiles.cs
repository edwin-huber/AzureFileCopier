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
    internal class CopyLocalStorageToAzureFiles : LocalFileSystemSourceCopy, IWork
    {
        private readonly CopierOptions opts;
        private readonly ITargetStorage azureFilesTargetStorage;

        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal CopyLocalStorageToAzureFiles(CopierOptions optsin) : base(optsin)
        {
            opts = optsin;
            azureFilesTargetStorage = new AzureFilesTargetStorage();
        }

        /// <summary>
        /// Starts the processing of work given the path provided to the tool
        /// If there are already messages in the folder queue, those will be processed first...
        /// </summary>
        /// <returns></returns>
        async void IWork.StartAsync()
        {
            // first enumerate top level and add to queue.
            // Need t
            PrepareBatchedProcessingAndQueues(opts);
            await ProcessAllWork().ConfigureAwait(false);

        }

 

        /// <summary>
        /// Processes work based on workitems found in the queues
        /// </summary>
        /// <returns></returns>
        private async Task ProcessAllWork()
        {
            // ToDo: Add Job / Queue Id to log events
            Log.Debug(FixedStrings.StartingFolderQueueLogJson, Thread.CurrentThread.Name);
            ProcessWorkQueue(WorkManager.folderCopyQueues[opts.WorkerId], false);
            
            Log.Debug(FixedStrings.StartingFileQueueLogJson, Thread.CurrentThread.Name);
            ProcessWorkQueue(WorkManager.fileCopyQueues[opts.WorkerId], true);

            Log.Debug(FixedStrings.StartingLargeFileQueueLogJson, Thread.CurrentThread.Name);
            ProcessWorkQueue(WorkManager.largeFileCopyQueue, true);
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
        /// // ToDo: Move this as strategy to WorkManager, pass Target storage copy function in as delegate
        private void ProcessWorkQueue(IWorkItemMgmt workQueue, bool isFileQueue)
        {
            int retryCount = 0;
            try
            {
                // we loop through several times, in case there are other workers still submitting stuff...
                while (retryCount < WorkManager.MaxQueueRetry)
                {
                    bool thereIsWork = WorkManager.IsThereWork(workQueue);

                    if (thereIsWork)
                    {
                        retryCount = 0;
                        List <WorkItem> workitems = WorkManager.GetWork(workQueue);

                        foreach (var workitem in workitems)
                        {
                            if (workitem != null && workitem.Empty == false)
                            {
                                if (isFileQueue)
                                {
                                    azureFilesTargetStorage.CopyFile(workitem.SourcePath, workitem.TargetPath);
                                }
                                else
                                {
                                    if (WorkManager.WasFolderAlreadyProcessed(workitem.SourcePath) == false)
                                    {
                                        Log.Debug(FixedStrings.CreatingDirectory + workitem.TargetPath, Thread.CurrentThread.Name);
                                        if (!azureFilesTargetStorage.CreateFolder(workitem.TargetPath))
                                        {
                                            Log.Always(ErrorStrings.FailedCopy + workitem.TargetPath);
                                        }
                                        WorkManager.SubmitFolderWorkitems(localFileStorage.EnumerateFolders(workitem.SourcePath), opts, base.AdjustTargetFolderPath);
                                        WorkManager.SubmitFileWorkItems(workitem.TargetPath, localFileStorage.EnumerateFiles(workitem.SourcePath));
                                        WorkManager.FinishedProcessingFolder(workitem.SourcePath);
                                    }
                                }
                            }
                        }
                        workQueue.CompleteWork();
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

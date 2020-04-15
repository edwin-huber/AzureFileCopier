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
using System.Text;
using System.Threading.Tasks;

namespace aafccore.work
{
    internal class CopyJob
    {
        protected AzureQueueWorkItemMgmt folderCopyQueue;
        protected readonly CloudStorageAccount cloudStorageAccount;
        protected AzureQueueWorkItemMgmt fileCopyQueue;
        protected readonly AzureQueueWorkItemMgmt largeFileCopyQueue;
        protected readonly AzureFilesTargetStorage azureFilesTargetStorage;
        protected static ISetInterface folderDoneSet;
        protected readonly double largeFileSize = Configuration.Config.GetValue<double>(ConfigStrings.LARGE_FILE_SIZE_BYTES);
        protected readonly int MaxQueueRetry = Configuration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);

        protected readonly int originalWorkerId;
        protected int batchLength;
        protected int topLevelFoldersCount;

        // Polly Retry Control
        protected static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.MAX_RETRY);
        protected static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        protected readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

        protected CopyJob(CloudStorageAccount cloudStorageAccountIn, CopierOptions opts)
        {
            // Folder WorkItem mgmt needs late init, as we don't need more queues than folders!
            cloudStorageAccount = cloudStorageAccountIn;
            largeFileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.LargeFilesQueueName, true);
            azureFilesTargetStorage = new AzureFilesTargetStorage();
            folderDoneSet = AzureServiceFactory.GetFolderDoneSet();
            originalWorkerId = opts.WorkerId;
        }

        /// <summary>
        /// Provides the number of folders to be copied in this batch 
        /// </summary>
        /// <returns></returns>
        protected int GetBatchLength(int totalFolders, CopierOptions opts)
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
        protected int GetBatchStartingIndex(int totalFolders, CopierOptions opts)
        {
            return (totalFolders / opts.WorkerCount) * opts.WorkerId;
        }

        protected async Task<bool> FolderWasNotAlreadyCompleted(WorkItem workItem)
        {
            var done = await folderDoneSet.IsMember(workItem.SourcePath).ConfigureAwait(true);
            return !done;
        }

        protected async Task<List<WorkItem>> GetWork(AzureQueueWorkItemMgmt workQueue)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await workQueue.Fetch().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        protected async Task<bool> IsThereWork(AzureQueueWorkItemMgmt workQueue)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await workQueue.WorkAvailable().ConfigureAwait(true);
            }).ConfigureAwait(false);
        }

    }
}

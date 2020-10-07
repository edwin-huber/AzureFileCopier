using aafccore.resources;
using aafccore.util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace aafccore.work
{
    /// <summary>
    /// Work items are distrbuted across all queues, but now only fetched from a single queue.
    /// This allows us to optimize message processing per queue and type, and ensure optimal 
    /// distribution of work across all queues and workers.
    /// This is necessary, as we have found very unbalanced trees in the directory structures being copied to Azure.
    /// </summary>
    internal class AzureWorkItemSubmissionController : IWorkItemController
    {
        private List<AzureQueueWorkItemMgmt> AzureFileQueueManagers = new List<AzureQueueWorkItemMgmt>();
        private List<AzureQueueWorkItemMgmt> AzureFolderQueueManagers = new List<AzureQueueWorkItemMgmt>();
        private AzureQueueWorkItemMgmt AzureLargeFileQueueManager;
        private static readonly object locker = new object();
        private int WorkerCount = 1;
        private int CurrentFileManangerIndex = 0;
        private int CurrentFolderManangerIndex = 0;

        internal AzureWorkItemSubmissionController(int workerCount, int workerId)
        {
            WorkerCount = workerCount;
            initializeWorkItemMgmtAndQueues(workerCount);
            if (CurrentFileManangerIndex > 1)
            {
                CurrentFileManangerIndex = workerId - 1;
            }
            if (CurrentFolderManangerIndex > 1)
            {
                CurrentFolderManangerIndex = workerId - 1;
            }
        }

        private void initializeWorkItemMgmtAndQueues(int workerCount)
        {
            for(int i = 0; i < workerCount; i++)
            {
                AzureFileQueueManagers.Add(new AzureQueueWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + i, false));
                AzureFolderQueueManagers.Add(new AzureQueueWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + i, false));
            }
            AzureLargeFileQueueManager = new AzureQueueWorkItemMgmt(CloudObjectNameStrings.LargeFilesQueueName, true);
            
        }
                
        
        public Task<bool> SubmitFile(WorkItem workitem)
        {
            lock (locker)
            {
                CurrentFileManangerIndex++;

                if (CurrentFileManangerIndex >= WorkerCount)
                {
                    CurrentFileManangerIndex = 0;
                }
            }
            Log.Always("FILE : " + workitem.SourcePath + " submitted to " + CurrentFileManangerIndex);
            return AzureFileQueueManagers[CurrentFileManangerIndex].Submit(workitem);
            
        }

        public Task<bool> SubmitFolder(WorkItem workitem)
        {
            lock (locker)
            {
                CurrentFolderManangerIndex++;

                if (CurrentFolderManangerIndex >= WorkerCount)
                {
                    CurrentFolderManangerIndex = 0;
                }
            }
            Log.Always("FOLDER : " + workitem.SourcePath + " submitted to " + CurrentFileManangerIndex);
            return AzureFolderQueueManagers[CurrentFolderManangerIndex].Submit(workitem);
        }

        public Task<bool> SubmitLargeFile(WorkItem workitem)
        {
            return AzureLargeFileQueueManager.Submit(workitem);
        }

    }
}

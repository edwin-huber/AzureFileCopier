using aafccore.control;
using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Azure.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.work
{
    /// <summary>
    /// Simple and Naieve Monitor implementation
    /// Loops through queues at set interval, updating stats of each queue
    /// </summary>
    internal static class QueueMonitor
    {
        private static int UpdateInterval;

        private static int largestFileQueue = 0;
        private static int largestFolderQueue = 0;
        private static int largeFilesQueueLength = 0;
        private static int totalFolderMessages = 0;
        private static int totalFileMessages = 0;
        private static int CurrentStattColumnOffset = 0;

         internal static Task Start(int updateInterval, int workerCount)
        {


            if (updateInterval > 10)
            {
                UpdateInterval = 1000 * updateInterval;
            }
            else
            {
                UpdateInterval = 10000;
            }
            
            // loops based on interval - min 10 seconds
            while (true)
            {
                // loop through all queues
                // 2 options: 
                // 1. maintain lists of all queue
                // 2. loop through all queue dynamically
                totalFolderMessages = 0;
                totalFileMessages = 0;
                CurrentStattColumnOffset = 0;
                UpdateLargeFileStats();

                for (int i = 0; i < workerCount; i++)
                {
                    UpdateFolderStats(i);
                    UpdateFileStats(i);

                    // update folder queues 
                }

                
                // update file queues
                Thread.Sleep(UpdateInterval);
            }


        }

   
        private static async void UpdateFolderStats(int Id)
        {
            var folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + Id.ToString());
            var sizeFolderQueue = await GetQueueSize(folderCopyQueue).ConfigureAwait(true);
            if (sizeFolderQueue > 0)
            {                
                totalFolderMessages += sizeFolderQueue;
                if (sizeFolderQueue > largestFolderQueue)
                {
                    largestFolderQueue = sizeFolderQueue;
                }
            }
        }

        private static async void UpdateFileStats(int Id)
        {
            // ToDo: we need to reduce the number of object creations... use an iterable collection to store all queues
            // questions around efficiency and connection limits... ?
            var folderCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + Id.ToString());
            var sizeFileQueue = await GetQueueSize(folderCopyQueue).ConfigureAwait(false);
            if (sizeFileQueue > 0)
            {
                totalFileMessages += sizeFileQueue;
                if (sizeFileQueue > largestFileQueue)
                {
                    largestFileQueue = sizeFileQueue;
                }
            }
        }

        private static async void UpdateLargeFileStats()
        {
            var largeFileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.LargeFilesQueueName);
            var sizeLargeFilesQueue = await GetQueueSize(largeFileCopyQueue).ConfigureAwait(false);
            if (sizeLargeFilesQueue > 0)
            {
                totalFileMessages += sizeLargeFilesQueue;
                if (sizeLargeFilesQueue > largeFilesQueueLength)
                {
                    largestFolderQueue = sizeLargeFilesQueue;
                }
            }
        }



        private static async Task<int> GetQueueSize(IWorkItemMgmt workItemSource)
        {
            return await workItemSource.GetCountOfOutstandingWork().ConfigureAwait(false);
        }
    }
}

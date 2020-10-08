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
    internal class QueueMonitor : IWork
    {
        private int UpdateInterval;
        private int WorkerCount;
        private int LongestFileQueueLength;
        private int LongestFolderQueueLength;
        private int LargeFilesQueueLength;
        private int TotalFolderMessages;
        private int TotalFileMessages;
        private IWorkItemMgmt[] folderWorkQueues;
        private IWorkItemMgmt[] fileWorkQueues;
        private IWorkItemMgmt largeFileCopyQueue;

        internal QueueMonitor(int updateInterval, int workerCount)
        {
            if (updateInterval > 10)
            {
                UpdateInterval = 1000 * updateInterval;
            }
            else
            {
                UpdateInterval = 10000;
            }
            WorkerCount = workerCount;
            folderWorkQueues = new AzureQueueWorkItemMgmt[WorkerCount];
            fileWorkQueues = new AzureQueueWorkItemMgmt[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                folderWorkQueues[i] = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFolderQueueName + i);
                fileWorkQueues[i] = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.CopyFilesQueueName + i);
            }

            largeFileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.LargeFilesQueueName);
        }

        public void StartAsync()
        {
            // loops based on interval - min 10 seconds
            while (true)
            {
                // loop through all queues
                // 2 options: 
                // 1. maintain lists of all queue
                // 2. loop through all queue dynamically
                TotalFolderMessages = 0;
                TotalFileMessages = 0;
                LongestFileQueueLength = 0;
                LongestFolderQueueLength = 0;
                LargeFilesQueueLength = 0;
                
                UpdateLargeFileStats();

                for (int i = 0; i < WorkerCount; i++)
                {
                    UpdateFolderStats(i);
                    UpdateFileStats(i);

                }

                // ToDo: Fix the logging
                Log.Always("Queue Stats\": {\"LongestFileQueueLength\":\"" + LongestFileQueueLength + "\"" +
                    ",\"LongestFolderQueueLength\":\"" + LongestFolderQueueLength + "\"" +
                    ",\"LargeFilesQueueLength\":\"" + LargeFilesQueueLength + "\"" +
                    ",\"TotalFolderMessages\":\"" + TotalFolderMessages + "\"" +
                    ",\"TotalFileMessages\":\"" + TotalFileMessages + "\"");

                if(LongestFileQueueLength == 0 &&
                    LongestFolderQueueLength == 0 && 
                    LargeFilesQueueLength == 0 &&
                    TotalFolderMessages == 0 &&
                    TotalFileMessages == 0)
                {
                    Log.Always("All queues look empty, press any key to exit...");
                }

                Thread.Sleep(UpdateInterval);
            }


        }

   
        private void UpdateFolderStats(int id)
        {

            int sizeFolderQueue = GetQueueSize(folderWorkQueues[id]);
            if (sizeFolderQueue > 0)
            {                
                TotalFolderMessages += sizeFolderQueue;
                if (sizeFolderQueue > LongestFolderQueueLength)
                {
                    LongestFolderQueueLength = sizeFolderQueue;
                }
            }
        }

        // ToDo: Fix this shit
        private void UpdateFileStats(int id)
        {
            var sizeFileQueue = GetQueueSize(fileWorkQueues[id]);
            if (sizeFileQueue > 0)
            {
                TotalFileMessages += sizeFileQueue;
                if (sizeFileQueue > LongestFileQueueLength)
                {
                    LongestFileQueueLength = sizeFileQueue;
                }
            }
        }

        private void UpdateLargeFileStats()
        {
            var largeFileCopyQueue = WorkItemMgmtFactory.CreateAzureWorkItemMgmt(CloudObjectNameStrings.LargeFilesQueueName);
            LargeFilesQueueLength = GetQueueSize(largeFileCopyQueue);
            if (LargeFilesQueueLength > 0)
            {
                TotalFileMessages += LargeFilesQueueLength;
                if (LargeFilesQueueLength > LongestFileQueueLength)
                {
                    LongestFileQueueLength = LargeFilesQueueLength;
                }
            }
        }



        private int GetQueueSize(IWorkItemMgmt workItemSource)
        {
            return workItemSource.GetCountOfOutstandingWork().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}

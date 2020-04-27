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
    internal class Monitor
    {
        private readonly ConsoleWriter cw = new ConsoleWriter(Console.CursorLeft, Console.CursorTop);
        private readonly CloudStorageAccount cloudStorageAccount;
        private int UpdateInterval;

        private readonly int FolderStatsRowOffset = 8;
        private readonly int LargestFolderQueueStatsColOffset = 30;
        private readonly int LargestFolderQueueStatsRowOffset = 4;
        
        private readonly int FileStatsRowOffset = 12;
        private readonly int LargestFileQueueStatsColOffset = 30;
        private readonly int LargestFileQueueStatsRowOffset = 5;

        private readonly int LargeFileStatsRowOffset = 6;
        private readonly int LargeFileStatsColOffset = 30;

        private int largestFileQueue = 0;
        private int largestFolderQueue = 0;
        private int largeFilesQueueLength = 0;

        private string blanks = "                      ";

        MonitorOptions opts;
        internal Monitor(MonitorOptions options)
        {
            opts = options;
            cloudStorageAccount = AzureServiceFactory.ConnectToControlStorage();
            UpdateInterval = 1000 * opts.MonitorInterval;
        }

        internal Task Start()
        {
            cw.WriteAt("Close window to stop monitor. Update Interval at " + UpdateInterval + " Seconds...", 0, 0);
            cw.WriteAt("Largest Folder Queue Approx : ", 0, LargestFolderQueueStatsRowOffset);
            cw.WriteAt("Largest File Queue Approx : ", 0, LargestFileQueueStatsRowOffset);
            cw.WriteAt("Large File Queue Approx : ", 0, LargeFileStatsRowOffset);
            cw.WriteAt("Folder Queues: (Approx Number of Messages in 1000s)", 0, FolderStatsRowOffset - 1);
            cw.WriteAt("File Queues: (Approx Number of Messages in 1000s)", 0, FileStatsRowOffset - 1);

            // based on interval X - min 30 seconds
            while (true) {
                // loop through all queues
                // 2 options: 
                // 1. maintain lists of all queue
                // 2. loop through all queue dynamically
                for (int i = 0; i < opts.WorkerCount; i++)
                {
                    UpdateFolderStats(i);
                    UpdateFileStats(i);

                    // update folder queues 
                    cw.WriteAt(blanks, LargestFolderQueueStatsColOffset, LargestFolderQueueStatsRowOffset);
                    cw.WriteAt(largestFolderQueue.ToString(), LargestFolderQueueStatsColOffset, LargestFolderQueueStatsRowOffset);

                    // update file queues
                    cw.WriteAt(blanks, LargestFileQueueStatsColOffset, LargestFileQueueStatsRowOffset);
                    cw.WriteAt(largestFileQueue.ToString(), LargestFileQueueStatsColOffset, LargestFileQueueStatsRowOffset);

                    UpdateLargeFileStats();

                    largestFileQueue = 0;
                    largestFolderQueue = 0;
                }

                Thread.Sleep(UpdateInterval);
            }

            
        }

        private async void UpdateFolderStats(int Id)
        {
            var folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId, false);
            var sizeFolderQueue = await GetQueueSize(folderCopyQueue).ConfigureAwait(false);
            if (sizeFolderQueue > largestFolderQueue)
            {
                largestFolderQueue = sizeFolderQueue;
            } 
            string eval = EvalString(sizeFolderQueue);
            cw.WriteAt(eval, Id, FolderStatsRowOffset);
        }

        private async void UpdateFileStats(int Id)
        {
            var folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId, false);
            var sizeFileQueue = await GetQueueSize(folderCopyQueue).ConfigureAwait(false);
            if (sizeFileQueue > largestFileQueue)
            {
                largestFolderQueue = sizeFileQueue;
            }
            string eval = EvalString(sizeFileQueue);
            cw.WriteAt(eval, Id, FileStatsRowOffset);
        }

        private async void UpdateLargeFileStats()
        {
            var largeFileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.LargeFilesQueueName, false);
            var sizeLargeFilesQueue = await GetQueueSize(largeFileCopyQueue).ConfigureAwait(false);
            if (sizeLargeFilesQueue > largeFilesQueueLength)
            {
                largestFolderQueue = sizeLargeFilesQueue;
            }
            string eval = EvalString(sizeLargeFilesQueue);
            cw.WriteAt(blanks, LargeFileStatsColOffset, LargeFileStatsRowOffset);
            cw.WriteAt(eval, LargeFileStatsColOffset, LargeFileStatsRowOffset);
        }

        private static string EvalString(int sizeFolder)
        {
            string eval = "0";

            if (sizeFolder == 0)
            {
                eval = "0";
            }
            else if (sizeFolder < 2000)
            {
                eval = "1";
            }
            else if (sizeFolder < 3000)
            {
                eval = "2";
            }
            else if (sizeFolder < 4000)
            {
                eval = "3";
            }
            else if (sizeFolder > 4000)
            {
                eval = "S";
            }

            return eval;
        }

        private async Task<int> GetQueueSize(AzureQueueWorkItemMgmt azureStorageQueue)
        {
            return await azureStorageQueue.GetApproxQueueSize().ConfigureAwait(false);
        }
    }
}

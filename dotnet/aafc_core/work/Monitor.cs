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

        private int UpdateInterval;

        private readonly int FolderStatsRowOffset = 12;
        private readonly int LargestFolderQueueStatsColOffset = 30;
        private readonly int LargestFolderQueueStatsRowOffset = 4;
        
        private readonly int FileStatsRowOffset = 16;
        private readonly int LargestFileQueueStatsColOffset = 30;
        private readonly int LargestFileQueueStatsRowOffset = 5;

        private readonly int LargeFileStatsRowOffset = 6;
        private readonly int LargeFileStatsColOffset = 30;

        private readonly int TotalFolderStatsRowOffset = 7;
        private readonly int TotalFolderStatsColOffset = 30;

        private readonly int TotalFileStatsRowOffset = 8;
        private readonly int TotalFileStatsColOffset = 30;

        private int largestFileQueue = 0;
        private int largestFolderQueue = 0;
        private int largeFilesQueueLength = 0;
        private int totalFolderMessages = 0;
        private int totalFileMessages = 0;

        private string blanks = "                      ";

        MonitorOptions opts;
        internal Monitor(MonitorOptions options)
        {
            opts = options;
            
            if (opts.MonitorInterval > 10)
            {
                UpdateInterval = 1000 * opts.MonitorInterval;
            }
            else
            {
                UpdateInterval = 10000;
            }
        }

        internal Task Start()
        {
            CreateConsoleWindowHeaders();

            // loops based on interval - min 10 seconds
            while (true)
            {
                // loop through all queues
                // 2 options: 
                // 1. maintain lists of all queue
                // 2. loop through all queue dynamically
                totalFolderMessages = 0;
                totalFileMessages = 0;
                UpdateLargeFileStats();

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
                }

                // update totals
                cw.WriteAt(blanks, TotalFolderStatsColOffset, TotalFolderStatsRowOffset);
                cw.WriteAt(totalFolderMessages.ToString(), TotalFolderStatsColOffset, TotalFolderStatsRowOffset);

                // update file queues
                cw.WriteAt(blanks, TotalFileStatsColOffset, TotalFileStatsRowOffset);
                cw.WriteAt(totalFileMessages.ToString(), TotalFileStatsColOffset, TotalFileStatsRowOffset);
                largestFileQueue = 0;
                largestFolderQueue = 0;
                Thread.Sleep(UpdateInterval);
            }


        }

        private void CreateConsoleWindowHeaders()
        {
            cw.WriteAt("Close window to stop monitor. Update Interval at " + UpdateInterval + " Seconds...", 0, 0);
            cw.WriteAt("Largest Folder Queue Approx : ", 0, LargestFolderQueueStatsRowOffset);
            cw.WriteAt("Largest File Queue Approx : ", 0, LargestFileQueueStatsRowOffset);
            cw.WriteAt("Large File Queue Approx : ", 0, LargeFileStatsRowOffset);

            cw.WriteAt("Total Folder Messages Approx : ", 0, TotalFolderStatsRowOffset);
            cw.WriteAt("Total File Messages Approx : ", 0, TotalFileStatsRowOffset);

            cw.WriteAt("Approx number of messages x 1000, more than 4000 displays \"S\")", 0, FolderStatsRowOffset - 2);
            cw.WriteAt("Folder Queues: (Approx Number of Messages in 1000s)", 0, FolderStatsRowOffset - 1);
            cw.WriteAt("File Queues: (Approx Number of Messages in 1000s)", 0, FileStatsRowOffset - 1);
        }

        private async void UpdateFolderStats(int Id)
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
            string eval = EvalString(sizeFolderQueue);
            cw.WriteAt(eval, Id, FolderStatsRowOffset);
        }

        private async void UpdateFileStats(int Id)
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
            string eval = EvalString(sizeFileQueue);
            cw.WriteAt(eval, Id, FileStatsRowOffset);
        }

        private async void UpdateLargeFileStats()
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
            string eval = EvalString(sizeLargeFilesQueue);
            cw.WriteAt(blanks, LargeFileStatsColOffset, LargeFileStatsRowOffset);
            cw.WriteAt(eval, LargeFileStatsColOffset, LargeFileStatsRowOffset);
        }

        private static string EvalString(int sizeQueue)
        {
            string eval = "0";

            if (sizeQueue == 0 )
            {
                eval = "0";
            }
            else if (sizeQueue < 1000)
            {
                eval = ".";
            }
            else if (sizeQueue < 1500)
            {
                eval = "1";
            }
            else if (sizeQueue < 2500)
            {
                eval = "2";
            }
            else if (sizeQueue < 3500)
            {
                eval = "3";
            }
            else if (sizeQueue > 4000)
            {
                eval = "S";
            }

            return eval;
        }

        private async Task<int> GetQueueSize(IWorkItemMgmt workItemSource)
        {
            return await workItemSource.GetCountOfOutstandingWork().ConfigureAwait(false);
        }
    }
}

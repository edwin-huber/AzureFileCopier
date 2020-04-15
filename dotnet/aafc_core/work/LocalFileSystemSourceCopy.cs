using aafccore.control;
using aafccore.resources;
using aafccore.storagemodel;
using aafccore.util;
using Microsoft.Azure.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aafccore.work
{
    /// <summary>
    /// Class providing access to local file system as copy source
    /// </summary>
    internal class LocalFileSystemSourceCopy : CopyJob
    {
        internal readonly LocalFileStorage localFileStorage;
        internal readonly StringBuilder pathAdjuster = new StringBuilder(300);
        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal LocalFileSystemSourceCopy(CloudStorageAccount cloudStorageAccountIn, CopierOptions optsin) : base(cloudStorageAccountIn, optsin)
        {
            localFileStorage = new LocalFileStorage(optsin.ExcludeFolders.Split(',').ToList<string>(), optsin.ExcludeFiles.Split(",").ToList<string>());

        }

        /// <summary>
        /// Submits folder work items to the azure storage queue
        /// </summary>
        /// <param name="folders"></param>
        /// <returns></returns>
        protected async Task SubmitFolderWorkitems(List<string> folders, CopierOptions opts)
        {
            foreach (var folder in folders)
            {
                if (opts.FullCheck || (!await folderDoneSet.IsMember(folder).ConfigureAwait(false)))
                {
                    WorkItem workitem = new WorkItem() { TargetPath = AdjustTargetFolderPath(folder, opts), SourcePath = folder };
                    await folderCopyQueue.Submit(workitem).ConfigureAwait(true);
                }
            }
        }

        /// <summary>
        /// Adjusts the target folder path to the correct form, removing and inserting as necessary
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        protected string AdjustTargetFolderPath(string folder, CopierOptions opts)
        {
            pathAdjuster.Clear();
            if (!string.IsNullOrEmpty(opts.DestinationSubFolder))
            {
                pathAdjuster.Append(opts.DestinationSubFolder);
            }

            if (folder.Length < 1)
            {
                // no adjust
                Log.Always("Copying root folder");
            }
            else
            {
                if (folder[Directory.GetDirectoryRoot(folder).Length..].StartsWith(opts.PathToRemove, StringComparison.InvariantCulture))
                {
                    pathAdjuster.Append(folder[(Directory.GetDirectoryRoot(folder).Length + opts.PathToRemove.Length)..]);
                }
                else
                {
                    pathAdjuster.Append(folder[Directory.GetDirectoryRoot(folder).Length..]);
                }
                pathAdjuster.Replace('\\', '/');
            }

            return pathAdjuster.ToString().TrimStart('/');
        }

        /// <summary>
        /// Submits files to Azure Storage queues, based on file size
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        protected async Task SubmitFileWorkItems(string targetPath, List<string> files)
        {
            foreach (var file in files)
            {
                Log.Debug(FixedStrings.File + file);
                WorkItem workitem = new WorkItem() { TargetPath = targetPath, SourcePath = file };
                long length = new System.IO.FileInfo(file).Length;
                if (length > largeFileSize)
                {
                    await largeFileCopyQueue.Submit(workitem).ConfigureAwait(true);
                }
                else
                {
                    await fileCopyQueue.Submit(workitem).ConfigureAwait(true);
                }
            }
        }

        /// <summary>
        /// Needed when starting the job to only submit folders which will be processed 
        /// by the associated folder queue
        /// </summary>
        protected async void SubmitBatchedTopLevelWorkitems(CopierOptions opts)
        {
            var topLevelFolders = localFileStorage.EnumerateFolders(opts.Path);
            topLevelFoldersCount = topLevelFolders.Count;
            topLevelFolders.Sort();

            batchLength = GetBatchLength(topLevelFoldersCount, opts);
            int batchIndex = GetBatchStartingIndex(topLevelFoldersCount, opts);

            topLevelFolders = topLevelFolders.GetRange(batchIndex, batchLength);

            if (topLevelFoldersCount > opts.WorkerCount)
            {
                // We have more folders than workers, we assign queues based on Worker Id
                Log.Always(FixedStrings.ProcessingQueue + opts.WorkerId);
                folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + opts.WorkerId, false);
                fileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + opts.WorkerId, false);
            }
            else
            {
                // We have more workers than folders, we assign queues based on zero based folder index
                Log.Always(FixedStrings.ProcessingQueue + batchIndex);
                folderCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFolderQueueName + batchIndex, false);
                fileCopyQueue = new AzureQueueWorkItemMgmt(cloudStorageAccount, CloudObjectNameStrings.CopyFilesQueueName + batchIndex, false);
            }

            foreach (var folder in topLevelFolders)
            {
                WorkItem workitem = new WorkItem() { TargetPath = AdjustTargetFolderPath(folder, opts), SourcePath = folder };
                await folderCopyQueue.Submit(workitem).ConfigureAwait(true);
            }

            // we only want to copy the root files once
            if (opts.WorkerId == 0)
            {
                await SubmitFileWorkItems(AdjustTargetFolderPath(opts.Path, opts), localFileStorage.EnumerateFiles(opts.Path)).ConfigureAwait(false);
            }
        }

    }
}

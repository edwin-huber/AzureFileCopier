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
    /// Class providing access to local file system as copy source
    /// </summary>
    internal class LocalFileSystemSourceCopy
    {
        internal readonly ISourceStorage localFileStorage;
        internal readonly StringBuilder pathAdjuster = new StringBuilder(300);
        
        protected WorkManager workManager;


        protected readonly int originalWorkerId;
        protected int batchLength;
        protected int topLevelFoldersCount;


        /// <summary>
        /// Constructor initializes all neccessary objects used to control the copy job.
        /// </summary>
        /// <param name="optsin"></param>
        /// <param name="cloudStorageAccount"></param>
        internal LocalFileSystemSourceCopy(CopierOptions optsin) 
        {
            workManager = new WorkManager(optsin);


            originalWorkerId = optsin.WorkerId;
            localFileStorage = new LocalFileStorage(optsin.ExcludeFolders.Split(',').ToList<string>(), optsin.ExcludeFiles.Split(",").ToList<string>());
        }







        /// <summary>
        /// Needed when starting the job to only submit folders which will be processed 
        /// by the associated folder queue
        /// </summary>
        protected async void PrepareBatchedProcessingAndQueues(CopierOptions opts)
        {
            var topLevelFolders = localFileStorage.EnumerateFolders(opts.Path);
            topLevelFoldersCount = topLevelFolders.Count;
            topLevelFolders.Sort();

            batchLength = workManager.GetBatchLength(topLevelFoldersCount, opts);
            int batchIndex = workManager.GetBatchStartingIndex(topLevelFoldersCount, opts);

            topLevelFolders = topLevelFolders.GetRange(batchIndex, batchLength);

            workManager.CreateWorkerQueuesForBatchProcessing(opts, topLevelFoldersCount, batchIndex);

            if (!opts.Resume)
            {
                workManager.SubmitFolderWorkitems(topLevelFolders, opts, this.AdjustTargetFolderPath);

                // we only want to copy the root files once
                if (opts.WorkerId == 0)
                {
                    workManager.SubmitFileWorkItems(AdjustTargetFolderPath(opts.Path, opts), localFileStorage.EnumerateFiles(opts.Path));
                }
            }
        }



        /// <summary>
        /// Adjusts the target folder path to the correct form, removing and inserting as necessary
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        internal string AdjustTargetFolderPath(string folder, CopierOptions opts)
        {
            pathAdjuster.Clear();
            if (!string.IsNullOrEmpty(opts.DestinationSubFolder))
            {
                pathAdjuster.Append(opts.DestinationSubFolder);
            }

            if (folder.Length < 1)
            {
                // no adjust
                Log.Debug("Copying root folder", Thread.CurrentThread.Name);
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
    }
}

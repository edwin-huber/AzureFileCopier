
using aafccore.control;
using aafccore.resources;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.File;
using Microsoft.Extensions.Configuration;
using System;

namespace aafccore.servicemgmt
{
    /// <summary>
    /// Service Factory requires an Init() call to be ready to create objects.
    /// The service factory uses Lazy<T> initialization for the cloud objects, which allows us 
    /// to defer their creation to only those cases that need it.
    /// 
    /// </summary>
    internal static class AzureServiceFactory
    {
        private static int WorkerCount;
        private static readonly object locker = new object();
        
        private static CloudFileShare cfs;

        internal static void Init(int workerCount)
        {
            lock (locker)
            {
                cfs = ConnectToFileStorage().GetShareReference(Configuration.Config.GetValue<string>(ConfigStrings.TargetAzureFilesShareName));
                WorkerCount = workerCount;
            }
        }

        private static readonly Lazy<CloudStorageAccount> ControlStorageAccount = new Lazy<CloudStorageAccount>(() =>
        {
            return AzureServiceManager.ConnectAzureStorage(ConnectionType.control);
        });

        public static Lazy<CloudStorageAccount> TargetStorageAccount = new Lazy<CloudStorageAccount>(() =>
        {
            return AzureServiceManager.ConnectAzureStorage(ConnectionType.target);
        });

        private static readonly Lazy<AzureRedisSet> FolderDoneSet = new Lazy<AzureRedisSet>(() =>
        {
            return new AzureRedisSet(CloudObjectNameStrings.folderAnalyzedSetName);
        });

        private static Lazy<AzureRedisStack> CopyStructureStack(int num)
        {
            return new Lazy<AzureRedisStack>(() =>
            {
                string stackName = "";
                if (WorkerCount > 1)
                {
                    stackName = CloudObjectNameStrings.copyStructureStackName + num + "s";
                }
                else
                {
                    stackName = CloudObjectNameStrings.copyStructureStackName;
                }
                Log.Debug(FixedStrings.UsingRedisListKey + stackName);
                return new AzureRedisStack(stackName);
            });
        }

        internal static CloudFileClient ConnectToFileStorage()
        {
            Log.Always(FixedStrings.ConnectingToCloudShare);
            return TargetStorageAccount.Value.CreateCloudFileClient();
        }

        internal static CloudStorageAccount ConnectToControlStorage()
        {
            Log.Always(FixedStrings.ConnectingToControl);
            return ControlStorageAccount.Value;
        }

        internal static AzureStorageQueue ConnectToAzureStorageQueue(string queueName, bool largeFiles)
        {
            return new AzureStorageQueue(ControlStorageAccount.Value, queueName, largeFiles);
        }

        internal static ISetInterface GetFolderDoneSet()
        {
            return FolderDoneSet.Value;
        }

        /// <summary>
        /// ToDo: Currently not using the stack, remove if we don't need it
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        internal static IStackInterface GetCopyStructureStack(int num)
        {
            return CopyStructureStack(num).Value;
        }

        private static AzureStorageQueue CopyFilesQueue(int i)
        {
            return ConnectToAzureStorageQueue(CloudObjectNameStrings.CopyFilesQueueName + i, false);
        }

        internal static AzureStorageQueue GetFileCopyQueue(int i)
        {
            return CopyFilesQueue(i);
        }

        private static AzureStorageQueue CreateFoldersQueue()
        {
            return ConnectToAzureStorageQueue(CloudObjectNameStrings.CopyFolderQueueName, false);
        }

        internal static AzureStorageQueue GetFolderStructureQueue(int queueNum)
        {
            return CreateFoldersQueue(queueNum);
        }
        private static AzureStorageQueue CreateFoldersQueue(int queueNum)
        {
            return ConnectToAzureStorageQueue(CloudObjectNameStrings.CopyFolderQueueName + queueNum, false);
        }

        private static readonly Lazy<AzureStorageQueue> LargeFilesQueue = new Lazy<AzureStorageQueue>(() =>
        {
            return ConnectToAzureStorageQueue(CloudObjectNameStrings.LargeFilesQueueName, true);
        });

        internal static AzureStorageQueue GetLargeFilesQueue()
        {
            return LargeFilesQueue.Value;
        }

        internal static CloudFileShare GetShareReference()
        {
            return cfs;
        }

    }
}

using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace aafccore.storagemodel
{
    /// <summary>
    /// Currently only implemented as target storage, with local file system as source
    /// If / when implementing multi source copy, we would need to change the model and object hierarchy
    /// this will however also be very SDK driven.
    /// </summary>
    internal class AzureBlobTargetStorage : ITargetStorage
    {
        private CloudBlobClient AzureBlobClient;
        private CloudBlobContainer AzureBlobContainer;
        private Stopwatch sw = new Stopwatch();
        readonly StorageCredentials storageCreds;
        internal AzureBlobTargetStorage()
        {
            storageCreds = new StorageCredentials(CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetStorageAccountName), CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetStorageKey));
            AzureBlobClient = new CloudBlobClient(new Uri(AzureServiceFactory.TargetStorageAccount.Value.BlobStorageUri.PrimaryUri.ToString()), storageCreds);
            AzureBlobContainer = AzureBlobClient.GetContainerReference(CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetAzureBlobContainerName));
        }


        /// <summary>
        /// Copys a file from a local path to target path
        /// </summary>
        /// <param name="sourceFilePath">path in the local file system</param>
        /// <param name="targetFolderPath">path in the form of folder/subfolder_level1/subfolder_level2</param>
        /// <returns></returns>
        public bool CopyFile(string sourceFilePath, string targetFilePath)
        {
            sw.Reset();
            string fileName = Path.GetFileName(sourceFilePath);
            var targetUri = new Uri(AzureBlobContainer.Uri + "/" + targetFilePath + "/" + fileName);
            var blockBlob = new CloudBlockBlob(targetUri, AzureBlobClient.Credentials);

            bool succeeded = false;
            sw.Start();
            try
            {
                blockBlob.UploadFromFileAsync(sourceFilePath).Wait();
                succeeded = true;
            }
            catch (Exception e)
            {
                Log.Always(e.Message, Thread.CurrentThread.Name);
            }
            sw.Stop();
            Log.Always(FixedStrings.CopyingFileJson + sourceFilePath + FixedStrings.TimeTakenJson + sw.ElapsedMilliseconds, Thread.CurrentThread.Name);
            return succeeded;
        }

        /// <summary>
        /// We cannot create folders in blob storage, this is implicit in the file prefix.
        /// </summary>
        /// <param name="folderPathToCreate">a simple folder path in the form folder/subfolder_level1/subfolder_level2</param>
        /// <returns></returns>
        public bool CreateFolder(string folderPathToCreate)
        {
            throw new Exception("Folders do not exist in blob storage");
        }
    }
}

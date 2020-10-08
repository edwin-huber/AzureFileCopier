using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.File;
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
    internal class AzureFilesTargetStorage : ITargetStorage
    {
        internal string AzureFileStorageUri { get; set; }
        private Stopwatch sw = new Stopwatch();
        readonly StorageCredentials storageCreds;
        internal AzureFilesTargetStorage()
        {
            storageCreds = new StorageCredentials(CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetStorageAccountName), CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetStorageKey));
            AzureFileStorageUri = AzureServiceFactory.TargetStorageAccount.Value.FileStorageUri.PrimaryUri.ToString() + CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetAzureFilesShareName);
        }


        /// <summary>
        /// Copys a file from a local path to target path
        /// </summary>
        /// <param name="sourceFilePath">path in the local file system</param>
        /// <param name="targetFolderPath">path in the form of folder/subfolder_level1/subfolder_level2</param>
        /// <returns></returns>
        public bool CopyFile(string sourceFilePath, string targetFolderPath)
        {
            sw.Reset();
            string fileName = Path.GetFileName(sourceFilePath);
            CloudFileDirectory cloudDirectory = new CloudFileDirectory(new Uri(AzureFileStorageUri + "/" + targetFolderPath), storageCreds);
            CloudFile destinationFile = cloudDirectory.GetFileReference(fileName);
            bool succeeded = false;
            sw.Start();
            try
            {
                destinationFile.UploadFromFileAsync(sourceFilePath).Wait();
                succeeded = true;
            }
            catch (Exception e)
            {
                Log.Always(e.Message);
            }
            sw.Stop();
            Log.Always(FixedStrings.CopyingFileJson + sourceFilePath + FixedStrings.TimeTakenJson + sw.ElapsedMilliseconds);
            return succeeded;
        }

        /// <summary>
        /// Creates a folder in Azure Files, epxects a forward slash based path
        /// </summary>
        /// <param name="folderPathToCreate">a simple folder path in the form folder/subfolder_level1/subfolder_level2</param>
        /// <returns></returns>
        public bool CreateFolder(string folderPathToCreate)
        {
            bool succeeded = false;
            bool storageException = false;
            string uri = AzureFileStorageUri + "/" + folderPathToCreate;
            try
            {
                CloudFileDirectory cloudDirectory = new CloudFileDirectory(new Uri(uri), storageCreds);
                succeeded = cloudDirectory.CreateIfNotExists(null, null);
                if (succeeded == false)
                {
                    succeeded = cloudDirectory.Exists();
                }
            }
            catch (StorageException se)
            {
                Log.Debug(se.Message, Thread.CurrentThread.Name);
                storageException = true;
            }
            catch (Exception e)
            {
                Log.Always("exception :" + e.Message);
            }

            if (storageException)
            {
                // we probably started with a folder depth greater than root and need to create the parentpaths
                succeeded = CreateWithParentHierachy(folderPathToCreate);
            }

            if (succeeded == false)
            {

            }
            return succeeded;
        }

        private bool CreateWithParentHierachy(string folderPathToCreate)
        {
            bool succeeded = false;
            string remotePathToCreate = "";
            try
            {
                foreach (var folder in folderPathToCreate.Split("/"))
                {
                    remotePathToCreate += "/" + folder;
                    CloudFileDirectory cloudDirectory = new CloudFileDirectory(new Uri(AzureFileStorageUri + remotePathToCreate), storageCreds); //  account.FileStorageUri.PrimaryUri.ToString() ;// "https://asynccopiertesttarget.file.core.windows.net/aafc1";
                    succeeded = cloudDirectory.CreateIfNotExists(null, null);
                }
            }
            catch (Exception e)
            {
                Log.Always("exception :" + e.Message);
                throw;
            }

            return succeeded;
        }

    }
}

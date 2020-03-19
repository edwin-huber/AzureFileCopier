using aafccore.resources;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.File;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

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

        readonly StorageCredentials storageCreds;
        internal AzureFilesTargetStorage()
        {
            storageCreds = new StorageCredentials(Configuration.Config.GetValue<string>(ConfigStrings.TargetStorageAccountName), Configuration.Config.GetValue<string>(ConfigStrings.TargetStorageKey));
            AzureFileStorageUri = AzureServiceFactory.TargetStorageAccount.Value.FileStorageUri.PrimaryUri.ToString() + Configuration.Config.GetValue<string>(ConfigStrings.TargetAzureFilesShareName);
        }


        /// <summary>
        /// Copys a file from a local path to target path
        /// </summary>
        /// <param name="sourceFilePath">path in the local file system</param>
        /// <param name="targetFolderPath">path in the form of folder/subfolder_level1/subfolder_level2</param>
        /// <returns></returns>
        public bool CopyFile(string sourceFilePath, string targetFolderPath)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            CloudFileDirectory cloudDirectory = new CloudFileDirectory(new Uri(AzureFileStorageUri + "/" + targetFolderPath), storageCreds);
            CloudFile destinationFile = cloudDirectory.GetFileReference(fileName);
            Log.Always(FixedStrings.CopyingFile + sourceFilePath);
            bool succeeded = false;
            try
            {
                destinationFile.UploadFromFile(sourceFilePath);
                succeeded = true;
            }
            catch (Exception e)
            {
                Log.Always(e.Message);
            }
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
                Log.Debug(se.Message);
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

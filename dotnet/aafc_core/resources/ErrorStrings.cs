namespace aafccore.resources
{
    internal static class ErrorStrings
    {
        // error texts
        internal const string ErrorCopyFileException = "ERROR: Copy File Exception!";
        internal const string ErrorWrongNumberArgs = "ERROR: Wrong number of args, please check the help!";
        internal const string ErrorNoValidModeSelected = "ERROR: No valid mode specified, exiting";
        internal const string ErrorUnableToConnectToShare =
            "ERROR: Unable to connect to Azure Storage Files share! Please make sure that it exists!";
        internal const string ErrorUnableToConnectToStorageAccount =
            "ERROR: Invalid storage account information provided. Please confirm the AccountName and AccountKey " +
            "are valid in the app.config file - then restart the sample.";
        internal const string ErrorInvalidPath = "ERROR: Invalid Path or local path not found : ";
        internal const string ErrorCreateFolderException = "ERROR! Create Folders Exception!";
        internal const string ErrorProcessingWorkException = "ERROR! Processing Work!";
        internal const string ErrorInvalidDirectoryLength = "ERROR! Invalid directory length";
        internal const string ErrorCloudDirectoryObjectInvalid = "CloudFileDirectory object invalid";
        internal const string ErrorInvalidFileSystemObjectGotFile = "Wrong object type, was expecting a folder, got a file!";
        internal const string ErrorInvalidFileSystemObjectGotFolder = "Wrong object type, was expecting a folder, got a folder!";
        internal const string ErrorFileDoesNotExist = "File Did not exist, or I was unable to access it when trying to copy it";
        internal const string FailedCopy = "########### FAILED TO COPY ########## ";
    }
}

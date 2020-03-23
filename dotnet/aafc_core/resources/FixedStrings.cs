namespace aafccore.resources
{
    /// <summary>
    /// All static / fixed strings are defined here or in similar classes to allow the solution to be localized
    /// if needed.
    /// </summary>
    internal static class FixedStrings
    {
        // cmdline args
        internal const string AnalyzeMode = "analyze";
        internal const string AdvancedMode = "advanced";
        internal const string FolderCopyMode = "folder";
        internal const string FileCopyMode = "file";
        internal const string LargeFileCopyMode = "largefile";
        internal const string HelpMode = "help";
        internal const string QuestionMark = "?";
        internal const string Reverse = "reverse";
        internal const string QuietMode = "quietmode";

        // cmdline output
        internal const string AnalyzeModeActivatedText = "Starting directory analysis with folder : ";
        internal const string AdvancedModeActivatedText = "ADVANCED MODE";
        internal const string FolderCopyModeActivatedText = "Starting folder copy mode...";
        internal const string ContentCopyModeActivatedText = "Starting content copy mode...";
        internal const string LargeFileCopyModeActivatedText = "Starting content copy mode for large files...";
        internal const string ResetModeActivatedText = "Resetting the cloud copy env!";
        internal const string HelpModeActivatedText = "Showing app help :";
        internal const string ArgsGivenText = "These are the app arguments you provided : ";
        internal const string AppFinished =
            "Not processing anything locally, there may be other workers and cloud data structures though!";
        internal const string CopyingFileJson = "Copying File\" : \"";
        internal const string TimeTakenJson = ",\"Time Taken\" : \"";
        internal const string MsJson = ",\"Ms\" : \"";
        internal const string GettingCloudRef = "Getting Cloud Ref to : ";
        internal const string TotalFolders = "Total Folders to Copy : ";
        internal const string BatchSize = "Batch size : ";
        internal const string NumberOfBatches = "Number of batches : ";
        internal const string BatchClient = "Batch client # : ";
        internal const string CopyingFromFolder = "Starting to copy from folder number ";
        internal const string ProcessingQueue = "Processing Queue ";
        internal const string PushingToStack = "Pushing onto stack : ";
        internal const string PushingToQueue = "Pushing onto queue : ";
        internal const string RanOutOfQueueMessages = "QueueDone\" : \"Ran out of messages in the queue to process.";
        internal const string CreatingDirectory = "Creating directory : ";
        internal const string File = "File : ";
        internal const string LargeFile = "Large File : ";
        internal const string ConnectingToCloudShare = "Connecting to cloud share";
        internal const string UsingRedisListKey = "Using Redis ListKey of ";
        internal const string FinishedJob = "Finished Job in ";
        internal const string Seconds = " Seconds.";
        internal const string ConnectingToControl = "Connecting to Azure Storage for copy control services";

        internal const string LogInfoSeparator = " INFO ";
        internal const string LogDebugSeparator = " DEBUG ";
        internal const string LogBlankSeparator = " - ";


        // mode help text
        internal const string HelpText = "Azure Async Copier usage:";
        internal const string HelpTextSeparator = "-------------------------------------------------------------";
        internal const string FolderCopyModeHelpText =
            "Creates the folders in the target Azure files share, and iterates through each folder, placing the paths " +
            "of the files to be copied in the file copy queue. All necessary configuration for this process is " +
            "stored in the appsettings.json";
        internal const string FolderCopyModeCommandExampleText = "Exmaple usage: \nazureasynccopier.exe folder";
        internal const string FileCopyModeHelpText =
            "Copies files to the target folders in the Azure files share. All necessary configuration for this " +
            "process is stored in the app.config";
        internal const string FileCopyModeCommandExampleText = "Exmaple usage: \nazureasynccopier.exe file";
        internal const string LargeFileCopyModeHelpText =
            "Copying large files takes much longer than small files, so we use a separate Azure storage queue for these files." +
            "On this queue we have a 5 minute timeout for the copying of large files.After 5 minutes, the copy message will " +
            "reappear in the queue. If your copy jobs take longer than 5 minutes, you can increase this timeout in the app.config." +
            " There are 2 settings for large files in the app.config:" +
            "\nLARGE_FILE_COPY_TIMEOUT" +
            "\nLARGE_FILE_SIZE_BYTES\n" +
            "Use these to tune your copier performance / throughput. Do not reduce the large file copy timeout, otherwise the " +
            "copier will just get stuck on large files and keep retrying them!";
        internal const string LargeFileCopyModeCommandExampleText = "Exmaple usage: \nazureasynccopier.exe largefile";
        internal const string AdvancedModeHelpText =
            "ADVANCED MODE: Same as ANALYZE mode, but uses batches, can be used to parallelize the analysis of complex " +
            "folder structures. Work is split based on the number of batches, and the running process is assigned a batch " +
            "client number, and will process the set of top level folders based on this client number.";
        internal const string AdvancedModeCommandExampleText =
            "Exmaple usage: \nCommand will split the folders under e:\\repo into 4 sets / batches, and assign this " +
            "instance of the copier to the first set. You cannot have more clients than sets of folders..." +
            "\nazureasynccopier.exe advanced e:\repo 4 1";

        internal const string PathToRemoveHelpText =
            "If you do not want the full path and folder structure copied to the target system, then provide a string which " +
            "will represent the path to be removed during copy using forward slash notation. " +
            "If your paths contain spaces, please enclose the command in quotes" +
            "i.e. root/subfolder1/subfolder2 or \"root/sub folder1/subfolder 2\"";

        internal const string ResetHelpText =
            "If you have made a mistake, or think you need to restart, feel free to reach out to me with issues / " +
            "questions on github. The copier will overwrite files by default, and jobs can be started / batched. " +
            "It maintains a set of copied folders, so as not to copy them again, and is not designed to sync changing " +
            "directory structures, which can be done with Azure File Sync. If you need to reset, just wipe out the " +
            "Azure Storage Queues and restart the redis cache to reset all cloud data structures.";

        internal const string TargetFileFileCopyModeHelpText =
            "Provide a sub directory in the target file share under which file structure shoul be copied";

        internal const string WorkerCountHelpText =
            "Specify the number of workers to use for the copy and analysis jobs";

        internal const string DestinationSubFolderHelpText =
            "Provide a subfolder path to copy to which is a sub folder of the destination / target";

        internal const string SourcePathFileCopyModeHelpText =
            "If you do not want the full root path of the file structure to be copied to the target share, " +
            "provide the root path to remove from the copy job.";

        internal const string QuietModeHelpText = "Reduces messages sent to standard output.";

        internal const string ResetModeHelpText = "Resets all cloud copy structures if previous run should start fresh rather from where it left off.";
        internal const string StartingFileQueueLogJson = "QueueStart\" : \"File Work Queue";
        internal const string StartingFolderQueueLogJson = "QueueStart\" : \"Folder Work Queue";
        internal const string StartingLargeFileQueueLogJson = "QueueStart\" : \"Large File Work Queue";
        internal const string QueueBackOff = "Queue Control\" : \"unable to lock onto message, too much competition, retrying";
        internal const string QueueEmptyMessageJson = "Queue Empty\" : \"Sleeping 10 Seconds before retry...";
    }
}

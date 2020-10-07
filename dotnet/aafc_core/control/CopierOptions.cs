using aafccore.resources;
using CommandLine;
using Newtonsoft.Json;
using System;

namespace aafccore.control
{
    public enum CopyMode
    {
        blob,
        file
    }

    // ToDo: New help text here
    [Verb("copy", HelpText = "<need new help text here>")]
    public class CopierOptions : ICopyOptions
    {
        [Option('p', "path", Required = true, HelpText = "Path containing directory and files to be analyzed and copied to Azure.")]
        public string Path { get; set; }

        [Option('m', "mode", Required = true, HelpText = "Destination Service for the copy jobs, Azure blob or file storage.")]
        public CopyMode Mode { get; set; }

        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option('q', "quietmode", Required = false,
          Default = false,
          HelpText = FixedStrings.QuietModeHelpText)]
        public bool QuietMode { get; set; } = false;

        [Option("destinationsubfolder", Required = false,
        HelpText = FixedStrings.DestinationSubFolderHelpText)]
        public string DestinationSubFolder { get; set; } = "";

        [Option("pathtoremove", Required = false,
        HelpText = FixedStrings.PathToRemoveHelpText)]
        public string PathToRemove { get; set; } = "";

        // ToDo: Max Count is currently arbitrary, and needs testing against Azure Storage to set reliably
        [Option('w', "workercount",
            Default = 1,
            Required = false,
        HelpText = FixedStrings.WorkerCountHelpText)]
        public int WorkerCount { get; set; } = 1;

        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option("batchmode", Required = false,
        Default = false,
        HelpText = "Will only start one of a subset of batches, used to start individual copy processes")]
        public bool BatchMode { get; set; } = false;

        // ToDo: Decide if we need to remove this option, or allow ops admins to have 
        // full control of behaviour
        [Option("batchclient", Required = false,
        HelpText = FixedStrings.BatchClient)]
        public int WorkerId { get; set; } = 0;

        [Option('f', "fullcheck", Required = false,
        Default = false,
        HelpText = "Will check all folder contents again, and not skip folders that have already been created")]
        public bool FullCheck { get; set; } = false;

        [Option('x', "excludefolders", Required = false, HelpText = "Exclude a comma separated list of folder paths.")]
        public string ExcludeFolders { get; set; } = "";

        [Option("excludefiles", Required = false, HelpText = "Exclude a comma separated list of files.")]
        public string ExcludeFiles { get; set; } = "";

        [Option("fileonly", Required = false,
        Default = false,
        HelpText = FixedStrings.FileOnlyMode)]
        public bool FileOnlyMode { get; set; } = false;

        [Option("largefileonly", Required = false,
        Default = false,
        HelpText = FixedStrings.LargeFileOnlyModeHelpText)]
        public bool LargeFileOnlyMode { get; set; } = false;

        [Option("resume", Required = false,
        Default = false,
        HelpText = FixedStrings.ResumeModeHelpText)]
        public bool Resume { get; set; } = false;

        [Option("filerunnerperfolderqueue",
        Default = 2,
        Required = false,
        HelpText = "The number of file runners to start per folder queue, maxed out usually at 4, better is more worker queues in general")]
        public int FileRunnerPerFolderQueue { get; set; } = 2;

        public bool Quiet()
        {
            return QuietMode;
        }

        public int Workers()
        {
            return WorkerCount;
        }

        public bool Batch()
        {
            return BatchMode;
        }

        public int NumFileRunnersPerQueue()
        {
            return FileRunnerPerFolderQueue;
        }
     }
}

using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    public abstract class CopierOptions
    {
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

        [Option("batchclient", Required = false,
        HelpText = FixedStrings.BatchClient)]
        public int WorkerId { get; set; } = 0;

        [Option('f', "fullcheck", Required = false,
        Default = false,
        HelpText = "Will check all folder contents again, and not skip folders that have already been created")]
        public bool FullCheck { get; set; } = false;
    }
}

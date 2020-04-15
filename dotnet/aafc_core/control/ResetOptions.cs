using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    [Verb("reset", HelpText = FixedStrings.ResetModeHelpText)]
    internal class ResetOptions
    {
        // ToDo: Max Count is currently arbitrary, and needs testing against Azure Storage to set reliably
        [Option('w', "workercount",
            Default = 1,
            Required = false,
        HelpText = FixedStrings.WorkerCountHelpText)]
        public int WorkerCount { get; set; } = 1;

        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option('q', "quietmode", Required = false,
          Default = false,
          HelpText = FixedStrings.QuietModeHelpText)]
        public bool QuietMode { get; set; } = false;

    }
}

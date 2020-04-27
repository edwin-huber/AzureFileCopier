using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    [Verb("monitor", HelpText = FixedStrings.FolderCopyModeHelpText)]
    public class MonitorOptions : CopierOptions
    {
        [Option("monitorinterval",
            Default = 10,
            Required = false,
        HelpText = "Interval in seconds in which to update stats.")]
        public int MonitorInterval { get; set; } = 10;
    }
}

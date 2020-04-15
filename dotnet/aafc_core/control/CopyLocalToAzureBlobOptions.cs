using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    [Verb("localtoblob", HelpText = FixedStrings.FolderCopyModeHelpText)]
    public class CopyLocalToAzureBlobOptions : CopierOptions
    {
        [Option('c', "container", Required = false, HelpText = "container to create")]
        public string Share { get; set; } = "";
    }
}

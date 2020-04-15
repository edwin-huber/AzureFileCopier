using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    [Verb("localtofiles", HelpText = FixedStrings.FolderCopyModeHelpText)]
    public class CopyLocalToAzureFilesOptions : CopierOptions
    {
        [Option('s', "share", Required = false, HelpText = "sharenametocreate")]
        public string Share { get; set; } = "";
    }
}

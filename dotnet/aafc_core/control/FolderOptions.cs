using aafccore.resources;
using CommandLine;

namespace aafccore.control
{
    [Verb("copy", HelpText = FixedStrings.FolderCopyModeHelpText)]
    public class FolderOptions : CopierOptions
    {
        [Option('p', "path", Required = true, HelpText = "Path containing directory and files to be analyzed and copied to Azure.")]
        public string Path { get; set; }

        [Option('x', "excludefolders", Required = false, HelpText = "Exclude a comma separated list of folder paths.")]
        public string ExcludeFolders { get; set; } = "";

        [Option("excludefiles", Required = false, HelpText = "Exclude a comma separated list of files.")]
        public string ExcludeFiles { get; set; } = "";

        [Option('s', "share", Required = false, HelpText = "sharenametocreate")]
        public string Share { get; set; } = "";
    }
}

using System.Collections.Generic;

namespace aafccore.storagemodel
{
    internal interface ISourceStorage
    {
        List<string> EnumerateFolders(string sourcePathFolder);
        List<string> EnumerateFiles(string sourcePathFolder);

        List<string> FolderExclusionList { get; set; }
        List<string> FileExclusionList { get; set; }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace aafccore.storagemodel
{
    internal class LocalFileStorage : ISourceStorage
    {
        public List<string> FolderExclusionList { get; set; }
        public List<string> FileExclusionList { get; set; }

        internal LocalFileStorage(List<string> folderExclusionList, List<string> fileExclusionList)
        {
            FolderExclusionList = folderExclusionList;
            FileExclusionList = fileExclusionList;
        }

        /// <summary>
        /// Returns an unsorted list of the files under the path provided
        /// removes files based on case sensitive exclusion list
        /// </summary>
        /// <param name="sourcePathFolder"></param>
        /// <returns></returns>
        public List<string> EnumerateFiles(string sourcePathFolder)
        {
            List<string> files = Directory.GetFiles(sourcePathFolder).ToList<string>();
            if (FileExclusionList != null)
            {
                foreach (var file in FileExclusionList)
                {
                    files.RemoveAll(r => r.EndsWith("/" + file, StringComparison.InvariantCulture));
                }
            }
            // not worth sorting files
            return files;
        }

 
        /// <summary>
        /// Returns a sorted list of folders under the path provided
        /// Removing folders in the exclude list
        /// </summary>
        /// <param name="sourcePathFolder"></param>
        /// <returns></returns>
        public List<string> EnumerateFolders(string sourcePathFolder)
        {
            List<string> topLevelFolders = Directory.GetDirectories(sourcePathFolder).ToList<string>();
            if (FolderExclusionList != null)
            {
                foreach (var folder in FolderExclusionList)
                {
                    topLevelFolders.Remove(folder);
                }
            }
            topLevelFolders.Sort();
            return topLevelFolders;
        }
    }
}

namespace aafccore.storagemodel
{
    internal interface ITargetStorage
    {
        bool CreateFolder(string folderPathToCreate);
        bool CopyFile(string sourceFile, string targetFolderPath);
    }
}

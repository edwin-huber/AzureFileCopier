using Microsoft.Azure.Storage;

namespace aafccore.servicemgmt
{
    /// <summary>
    /// Code is a little ugly while I wrangle the best method to manage the diverse resources
    /// the copier requires
    /// </summary>
    public static class AzureServiceManager
    {
        public static CloudStorageAccount ConnectAzureStorage(ConnectionType connectionType)
        {
            return AzureStorageManager.CreateStorageAccountFromConnectionString(connectionType);
        }
    }
}

using aafccore.resources;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace aafccore.servicemgmt
{
    public enum ConnectionType
    {
        target,
        control
    }
    public static class AzureStorageManager
    {
        /// <summary>
        /// Validates the connection string information in app.config and throws an exception if it looks like 
        /// the user hasn't updated this to valid values. 
        /// </summary>
        /// <returns>CloudStorageAccount object</returns>
        public static CloudStorageAccount CreateStorageAccountFromConnectionString(ConnectionType connectionType)
        {
            CloudStorageAccount storageAccount = null;

            try
            {
                switch (connectionType)
                {
                    case ConnectionType.control:
                        storageAccount = CloudStorageAccount.Parse(CopierConfiguration.Config.GetValue<string>(ConfigStrings.ControlStorageConnectionString));
                        break;
                    case ConnectionType.target:
                        storageAccount = CloudStorageAccount.Parse(CopierConfiguration.Config.GetValue<string>(ConfigStrings.TargetStorageConnectionString));
                        break;
                }

            }
            catch (FormatException fe)
            {
                Log.Always(fe.Message);
                Log.Always(ErrorStrings.ErrorUnableToConnectToStorageAccount);
                Environment.Exit(1);
            }
            catch (ArgumentException ae)
            {
                Log.Always(ae.Message);
                Log.Always(ErrorStrings.ErrorUnableToConnectToStorageAccount);
                Environment.Exit(1);
            }

            return storageAccount;
        }
    }
}

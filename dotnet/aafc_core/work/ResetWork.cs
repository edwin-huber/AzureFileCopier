using aafccore.control;
using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace aafccore.work
{

    class ResetWork
    {
        private readonly ResetOptions opts;
        private static ISetInterface folderDoneSet;
        private readonly AzureStorageQueue CopyFilesQueue;
        private readonly AzureStorageQueue LargeFilesQueue;

        public ResetWork(ResetOptions opts_in)
        {
            opts = opts_in;
            AzureServiceFactory.Init(opts);
            folderDoneSet = AzureServiceFactory.GetFolderDoneSet();

            CopyFilesQueue = AzureServiceFactory.GetFileCopyQueue();

            LargeFilesQueue = AzureServiceFactory.GetLargeFilesQueue();
        }

        public async Task Reset()
        {
            Log.Always("Resetting redis cache set");
            await folderDoneSet.Reset().ConfigureAwait(true);
            Log.Always("Resetting file copy queue");
            await CopyFilesQueue.Reset().ConfigureAwait(true);
            Log.Always("Resetting large file copy queue");
            await LargeFilesQueue.Reset().ConfigureAwait(true);
            for (int i = 0; i < opts.WorkerCount; i++)
            {
                Log.Always("Resetting structure queue " + i);
                await AzureServiceFactory.GetFolderStructureQueue(i).Reset().ConfigureAwait(false);
            }
        }
    }
}

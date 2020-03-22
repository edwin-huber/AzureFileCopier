using aafccore.control;
using aafccore.resources;
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
        private readonly AzureStorageQueue LargeFilesQueue;

        public ResetWork(ResetOptions opts_in)
        {
            opts = opts_in;
            AzureServiceFactory.Init(opts);
            folderDoneSet = AzureServiceFactory.GetFolderDoneSet();
            LargeFilesQueue = AzureServiceFactory.GetLargeFilesQueue();
        }

        public async Task Reset()
        {
            Log.Always("Resetting redis cache set");
            await folderDoneSet.Reset().ConfigureAwait(true);
            Log.Always("Resetting file copy queues");
            for (int i = 0; i < opts.WorkerCount; i++)
            {
                Log.Always("Resetting file queue " + i);
                await AzureServiceFactory.GetFileCopyQueue(i).Reset().ConfigureAwait(false);
            }
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

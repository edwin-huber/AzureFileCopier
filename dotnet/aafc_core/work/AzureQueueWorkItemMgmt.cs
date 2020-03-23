using aafccore.servicemgmt;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace aafccore.work
{
    /// <summary>
    /// This is not a threadsafe implementation
    /// Assumption is that each worker will work as a distinct process and rely on OS process scheduling
    /// for thread mgmt
    /// </summary>
    class AzureQueueWorkItemMgmt : IWorkItemMgmt
    {
        private List<CloudQueueMessage> CurrentQueueMessages { get; set; }
        private List<WorkItem> CurrentWorkItems { get; set; }
        private List<WorkItem> EmptyList = new List<WorkItem>();
        private readonly WorkItem Empty = new WorkItem() { Empty = true };
        private readonly AzureStorageQueue azureStorageQueue;

        /// <summary>
        /// Initializes Azure Queue Workitem mgmt
        /// </summary>
        /// <param name="account">CloudStorageAccount</param>
        /// <param name="queueName">Queue Name</param>
        /// <param name="largeFiles">Is this large file processing?</param>
        internal AzureQueueWorkItemMgmt(CloudStorageAccount account, string queueName, bool largeFiles)
        {
            azureStorageQueue = new AzureStorageQueue(account, queueName, largeFiles);
        }

        public async Task<bool> CompleteWork()
        {
            bool succeeded = false;
            if (CurrentQueueMessages != null && CurrentQueueMessages.Count > 0)
            {
                foreach (var message in CurrentQueueMessages)
                {
                    try
                    {

                        await azureStorageQueue.DeleteMessage(message).ConfigureAwait(true);

                        succeeded = true;
                    }
                    catch (AggregateException ae)
                    {
                        Log.Always(ae.Message);
                    }
                    catch (StorageException se)
                    {
                        Log.Always(se.Message);
                    }
                    catch (Exception e)
                    {
                        Log.Always(e.Message);
                        throw;
                    }
                }
            }
            CurrentQueueMessages = null;
            return succeeded;
        }

        public async Task<List<WorkItem>> Fetch()
        {
            try
            {
                if (CurrentQueueMessages == null)
                {
                    CurrentQueueMessages = await azureStorageQueue.DequeueSafe().ConfigureAwait(true);
                }
            }
            catch (StorageException se)
            {
                Log.Always(se.Message);
            }
            if (CurrentQueueMessages != null)
            {
                CurrentWorkItems = new List<WorkItem>();
                foreach (var message in CurrentQueueMessages)
                {
                    CurrentWorkItems.Add(JsonSerializer.Deserialize<WorkItem>(message.AsString));    
                }
                return CurrentWorkItems;
            }
            else
            {
                return new List<WorkItem>();
            }
        }

        public async Task<bool> Submit(WorkItem workitem)
        {
            bool succeeded = false;
            try
            {
                string message = JsonSerializer.Serialize(workitem);
                await azureStorageQueue.Enqueue(message).ConfigureAwait(true);
                succeeded = true;
            }
            catch (AggregateException ae)
            {
                Log.Always(ae.Message);
            }
            catch (StorageException se)
            {
                Log.Always(se.Message);
            }
            catch (Exception e)
            {
                Log.Always(e.Message);
                throw;
            }
            return succeeded;
        }

        /// <summary>
        /// If there are multiple processes or threads working on the same queue
        /// this result is not 100% reliable
        /// </summary>
        /// <returns></returns>
        public async Task<bool> WorkAvailable()
        {
            var empty = await azureStorageQueue.IsEmpty().ConfigureAwait(false);
            return !empty;
        }
    }
}

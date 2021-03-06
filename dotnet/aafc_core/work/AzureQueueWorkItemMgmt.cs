﻿using aafccore.servicemgmt;
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
    /// for thread mgmt.
    /// New implementation uses a distribution of work across several queues
    /// Each worker only pulls work from a single queue, but distributes work across several queues.
    /// </summary>
    class AzureQueueWorkItemMgmt : IWorkItemMgmt
    {
        private List<CloudQueueMessage> CurrentQueueMessages { get; set; }

        private List<WorkItem> CurrentWorkItems { get; set; }

        private readonly AzureStorageQueue azureStorageQueue;

        /// <summary>
        /// Initializes Azure Queue Workitem mgmt
        /// </summary>
        /// <param name="account">CloudStorageAccount</param>
        /// <param name="queueName">Queue Name</param>
        /// <param name="largeFiles">Is this large file processing?</param>
        internal AzureQueueWorkItemMgmt(string queueName, bool largeFiles)
        {
            azureStorageQueue = AzureServiceFactory.ConnectToAzureStorageQueue(queueName, largeFiles);
        }

        public async Task<bool> CompleteWork()
        {
            bool succeeded = false;
            if (CurrentQueueMessages != null && CurrentQueueMessages.Count > 0)
            {
                foreach (var workItemWithStatus in CurrentWorkItems)
                {
                    try
                    {
                        CloudQueueMessage queueMessageToComplete;

                        if (workItemWithStatus.Succeeded)
                        {
                            queueMessageToComplete = CurrentQueueMessages.Find(x => x.Id == workItemWithStatus.Id);
                            await azureStorageQueue.DeleteMessage(queueMessageToComplete).ConfigureAwait(true);
                        }


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
                    CurrentQueueMessages = new List<CloudQueueMessage>();
                    var queueMessages = await azureStorageQueue.DequeueSafe().ConfigureAwait(true);
                    foreach (var message in queueMessages)
                    {
                        CurrentQueueMessages.Add(message);
                    }
                }
            }
            catch (StorageException se)
            {
                Log.Always(se.Message);
            }
            if (CurrentQueueMessages != null)
            {
                CurrentWorkItems = new List<WorkItem>();
                foreach (var msg in CurrentQueueMessages)
                {
                    WorkItem work = JsonSerializer.Deserialize<WorkItem>(msg.AsString);
                    work.Id = msg.Id;
                    CurrentWorkItems.Add(work);
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
            bool empty = true;
            try
            {
                empty = await azureStorageQueue.IsEmpty().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
            }
            return !empty;
        }

        public async Task<int> GetCountOfOutstandingWork()
        {
            return await azureStorageQueue.FetchApproxQueueSize().ConfigureAwait(false);
        }

    }
}

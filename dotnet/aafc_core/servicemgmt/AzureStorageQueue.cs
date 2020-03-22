using aafccore.resources;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    class AzureStorageQueue : IQueueInterface
    {
        private CloudQueue queue;
        private readonly CloudStorageAccount storageAccount;
        private System.TimeSpan queueMessageHiddenTimeout;

        // Polly Retry Control
        private static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.MAX_RETRY);
        private static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        private readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

        public AzureStorageQueue(CloudStorageAccount storageAccount, string queueName, bool largeFiles)
        {
            this.storageAccount = storageAccount;
            TimeSpan hiddenMessageTimeout;
            if (largeFiles)
            {
                var timeout = Configuration.Config.GetValue<Int32>(ConfigStrings.LARGE_FILE_COPY_TIMEOUT);
                hiddenMessageTimeout = TimeSpan.FromMinutes(timeout);
            }
            else
            {
                hiddenMessageTimeout = TimeSpan.FromSeconds(60);
            }
            CreateQueue(queueName, hiddenMessageTimeout);
        }

        /// <summary>
        /// Creates the queue based on name given
        /// Should use redis lock for the case that there
        /// are multiple clients trying to create at the same time
        /// </summary>
        /// <param name="queueName"></param>
        private void CreateQueue(string queueName, TimeSpan messageHiddenTimeout)
        {
            var queueClient = storageAccount.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference(queueName);
            queueMessageHiddenTimeout = messageHiddenTimeout;
            queue.CreateIfNotExists();
        }

        /// <summary>
        /// Unsafe for storage queue, as message will reappear unless we delete it
        /// </summary>
        /// <returns></returns>
        public async Task<string> Dequeue()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                var result = await queue.GetMessageAsync().ConfigureAwait(true);
                return result.AsString;
            }).ConfigureAwait(true);
        }

        /// <summary>
        /// Use this in place of the standard dequeue due to queue semantics in Azure storage
        /// also using a jittering retry, as we need to account for multiple competing subscribers
        /// will retry dequeue operation 3 times before giving up 
        /// </summary>
        /// <returns></returns>
        public async Task<CloudQueueMessage> DequeueSafe()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                bool dequeueing = true;
                int retryCount = 0;
                CloudQueueMessage message = null;
                while (dequeueing)
                {
                    var tempMessage = await queue.PeekMessageAsync().ConfigureAwait(true);
                    if (tempMessage != null)
                    {
                        retryCount = 0;
                        // jittering the retrieval
                        Random rnd = new Random();
                        int sleepTime = (int)(rnd.NextDouble() * 500);
                        Thread.Sleep(sleepTime);
                        message = await queue.GetMessageAsync(queueMessageHiddenTimeout, null, null).ConfigureAwait(true);
                        if ((message == null || tempMessage == null) || (tempMessage.Id == message.Id))
                        {
                            dequeueing = false;
                        }
                        else
                        {
                            Log.Always(FixedStrings.QueueBackOff);
                        }
                    }
                    else
                    {
                        
                        if(retryCount < 5)
                        {
                            retryCount++;
                            Log.Always("QUEUE EMPTY: sleeping 10 Seconds before retry...");
                            Thread.Sleep(10000);
                        }
                        else
                        {
                            message = null;
                            dequeueing = false;
                        }
                    }

                }

                return message;
            }).ConfigureAwait(true);
        }

        /// <summary>
        /// Removes a message from Azure Storage queue based on the message ID and Pop Receipt
        /// Requires that the call has properly handled the Dequeuing of the message
        /// </summary>
        /// <param name="message"></param>
        public async Task DeleteMessage(CloudQueueMessage message)
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    await queue.DeleteMessageAsync(message.Id, message.PopReceipt).ConfigureAwait(true);
                }
                catch (AggregateException ae)
                {
                    Log.Always(ae.Message);
                }
                catch (Exception dm)
                {
                    Log.Always(dm.Message);
                }
            }).ConfigureAwait(true);
        }

        public async Task Enqueue(string message)
        {
            CloudQueueMessage cloudQueueMessage = new CloudQueueMessage(message);
            await retryPolicy.ExecuteAsync(async () =>
            {
                await queue.AddMessageAsync(cloudQueueMessage).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        /// <summary>
        /// This uses a peek to check if there is a message on the queue still, as simetimes
        /// the queue metadata was not updating, but we still had messages in there
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsEmpty()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                if (queue.ApproximateMessageCount == null || queue.ApproximateMessageCount == 0)
                {
                    var peek = await queue.PeekMessageAsync().ConfigureAwait(true);
                    if (peek != null)
                    {
                        return false;
                    }
                }
                return true;
            }).ConfigureAwait(true);
        }

        public async Task Reset()
        {
            await queue.ClearAsync().ConfigureAwait(true); //.DeleteIfExists();
        }
    }
}

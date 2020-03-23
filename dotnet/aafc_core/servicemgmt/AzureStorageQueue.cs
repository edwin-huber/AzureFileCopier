using aafccore.resources;
using aafccore.util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    class AzureStorageQueue : IQueueInterface
    {
        private CloudQueue queue;
        private readonly CloudStorageAccount storageAccount;
        private System.TimeSpan queueMessageHiddenTimeout;
        private static readonly int numberOfMessagesToDequeue = Configuration.Config.GetValue<int>(ConfigStrings.NUMBER_OF_MESSAGES_TO_DEQUEUE);
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
                var timeout = Configuration.Config.GetValue<Int32>(ConfigStrings.STANDARD_QUEUE_MESSAGE_TIMEOUT);
                hiddenMessageTimeout = TimeSpan.FromSeconds(timeout);
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
        public async Task<List<CloudQueueMessage>> DequeueSafe()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                bool dequeueing = true;
                int retryCount = 0;
                List<CloudQueueMessage> messages = null;
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
                        messages = (List <CloudQueueMessage>) await  queue.GetMessagesAsync(numberOfMessagesToDequeue, queueMessageHiddenTimeout, null, null).ConfigureAwait(true);
                        if ((messages == null || tempMessage == null) ||(messages.Count > 0) && (tempMessage.Id == messages[0].Id))
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
                            Log.Always(FixedStrings.QueueEmptyMessageJson);
                            Thread.Sleep(10000);
                        }
                        else
                        {
                            messages = null;
                            dequeueing = false;
                        }
                    }

                }

                return messages;
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

        /// <summary>
        /// Removes messages from Azure Storage queue based on the message ID and Pop Receipt
        /// Requires that the call has properly handled the Dequeuing of the message
        /// Might be better to delete messages on completion, rather than batching complete operations.
        /// </summary>
        /// <param name="message"></param>
        public async Task DeleteMessages(List<CloudQueueMessage> messages)
        {

            await retryPolicy.ExecuteAsync(async () =>
            {
                if (messages != null && messages.Count > 0)
                {
                    foreach (var message in messages)
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
                    }
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

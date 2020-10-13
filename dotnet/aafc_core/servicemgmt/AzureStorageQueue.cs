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
        private static readonly int numberOfMessagesToDequeue = CopierConfiguration.Config.GetValue<int>(ConfigStrings.NUMBER_OF_MESSAGES_TO_DEQUEUE);
        // Polly Retry Control
        private static readonly int maxRetryAttempts = CopierConfiguration.Config.GetValue<int>(ConfigStrings.QUEUE_MAX_RETRY);
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
                var timeout = CopierConfiguration.Config.GetValue<Int32>(ConfigStrings.LARGE_FILE_COPY_TIMEOUT);
                hiddenMessageTimeout = TimeSpan.FromMinutes(timeout);
            }
            else
            {
                var timeout = CopierConfiguration.Config.GetValue<Int32>(ConfigStrings.STANDARD_QUEUE_MESSAGE_TIMEOUT);
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
        public string Dequeue()
        {
                var result = queue.GetMessageAsync().Result;
                return result.AsString;
        }

        /// <summary>
        /// Use this in place of the standard dequeue due to queue semantics in Azure storage
        /// also using a jittering retry, as we need to account for multiple competing subscribers
        /// will retry dequeue operation 3 times before giving up 
        /// </summary>
        /// <returns></returns>
        public List<CloudQueueMessage> DequeueSafe()
        {
            bool dequeueing = true;
            int retryCount = 0;
            List<CloudQueueMessage> messages = null;
            while (dequeueing)
            {
                var tempMessage = queue.PeekMessage();
                if (tempMessage != null)
                {
                    retryCount = 0;
                    // jittering the retrieval
                    Random rnd = new Random();
                    int sleepTime = (int)(rnd.NextDouble() * 500);
                    Thread.Sleep(sleepTime);
                    // trying Synchronous Versions
                    // messages = (List <CloudQueueMessage>) await  queue.GetMessagesAsync(numberOfMessagesToDequeue, queueMessageHiddenTimeout, null, null).ConfigureAwait(true);
                    messages = (List<CloudQueueMessage>)queue.GetMessages(numberOfMessagesToDequeue, queueMessageHiddenTimeout, null, null);
                    if ((messages == null || tempMessage == null) || (messages.Count > 0) && (tempMessage.Id == messages[0].Id))
                    {
                        dequeueing = false;
                    }
                    else
                    {
                        if (messages.Count > 0)
                        {
                            Log.Always(FixedStrings.QueueBackOff);
                        }
                        Thread.Sleep(sleepTime);
                    }
                }
                else
                {
                    // ToDo: Evaluate if always retrying here is more efficient than returning up empty list
                    if (retryCount < maxRetryAttempts)
                    {
                        retryCount++;
                        Log.Always(FixedStrings.QueueEmptyMessageJson + queue.Name);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        messages = new List<CloudQueueMessage>(); // return empty list
                        dequeueing = false;
                        retryCount = 0;
                    }
                }

            }
            
            return messages;

        }

        /// <summary>
        /// Removes a message from Azure Storage queue based on the message ID and Pop Receipt
        /// Requires that the call has properly handled the Dequeuing of the message
        /// </summary>
        /// <param name="message"></param>
        public void DeleteMessage(CloudQueueMessage message)
        {
            // ToDo: Validate that this logic is safe...
            // might need to rethrow
            try
            {
                queue.DeleteMessage(message.Id, message.PopReceipt);
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

        /// <summary>
        /// Removes messages from Azure Storage queue based on the message ID and Pop Receipt
        /// Requires that the call has properly handled the Dequeuing of the message
        /// Might be better to delete messages on completion, rather than batching complete operations.
        /// </summary>
        /// <param name="message"></param>
        public async Task DeleteMessages(List<CloudQueueMessage> messages)
        {
            if (messages != null && messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    try
                    {
                        retryPolicy.ExecuteAsync(async () =>
                        {
                            await queue.DeleteMessageAsync(message.Id, message.PopReceipt).ConfigureAwait(true);

                        }).Wait();
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
        }


        public void Enqueue(string message)
        {
            CloudQueueMessage cloudQueueMessage = new CloudQueueMessage(message);
            retryPolicy.ExecuteAsync(async () =>
            {
                await queue.AddMessageAsync(cloudQueueMessage).ConfigureAwait(true);
            }).Wait();
        }

        /// <summary>
        /// This uses a peek to check if there is a message on the queue still, as simetimes
        /// the queue metadata was not updating, but we still had messages in there
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty()
        {
            
                if (queue.ApproximateMessageCount == null || queue.ApproximateMessageCount == 0)
                {
                    // ToDo: add a smaller loop instead of the polly retry to allow a sync processing
                    // need to see if and where yielding the threads is better
                    CloudQueueMessage peek = queue.PeekMessageAsync().Result;
                
                    if (peek != null)
                    {
                        return false;
                    }
                }
                return true;
            
        }

        public void Reset()
        {
            queue.ClearAsync().Wait(); 
            // queue.DeleteIfExistsAsync();
        }

        public int FetchApproxQueueSize()
        {
            queue.FetchAttributesAsync().Wait();
            return queue.ApproximateMessageCount ?? 0;
        }
    }
}

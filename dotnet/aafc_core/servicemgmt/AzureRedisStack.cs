using aafccore.resources;
using aafccore.util;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    public class AzureRedisStack : IStackInterface
    {
        static IDatabase db = null;
        private static readonly object locker = new object();
        private readonly string stackKey;

        // Polly Retry Control
        private static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.MAX_RETRY);
        private static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        private readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

        public AzureRedisStack(string key)
        {
            stackKey = key;
            if (db == null)
            {
                lock (locker)
                {
                    db = AzureRedisStack.RedisConnection.GetDatabase();
                }
            }
        }

        /// <summary>
        /// StackExchange Redisclient Method
        /// </summary>
        private static readonly Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            string cacheConnectionString = Configuration.Config.GetValue<string>(ConfigStrings.RedisConnectionString);
            return ConnectionMultiplexer.Connect(cacheConnectionString);
        });

        /// <summary>
        /// StackExchange Redisclient Method
        /// </summary>
        private static ConnectionMultiplexer RedisConnection
        {
            get
            {
                return lazyConnection.Value;
            }
        }


        async Task IStackInterface.ResetStack()
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                await db.ListTrimAsync(stackKey, 0, db.ListLength(stackKey)).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        async Task<long> IStackInterface.Push(string value)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                var response = await db.ListRightPushAsync(stackKey, value).ConfigureAwait(true);
                return response;
            }).ConfigureAwait(true);
        }

        async Task<string> IStackInterface.Pop()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await db.ListRightPopAsync(stackKey).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        async Task<bool> IStackInterface.IsEmpty()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await db.ListLengthAsync(stackKey).ConfigureAwait(true) == 0;
            }).ConfigureAwait(true);
        }
    }
}

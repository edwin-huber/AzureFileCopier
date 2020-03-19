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
    class AzureRedisSet : ISetInterface
    {

        static IDatabase db = null;
        // Needs to swtich to redis lock for any cross process contention
        // this only protects us in the case that we want to spawn a thread pool
        // and use multiple local tasks in an Async model
        private static readonly object locker = new object();

        private readonly string SetKey;

        // Polly Retry Control
        private static readonly int maxRetryAttempts = Configuration.Config.GetValue<int>(ConfigStrings.MAX_RETRY);
        private static readonly TimeSpan pauseBetweenFailures = TimeSpan.FromSeconds(10);
        private readonly AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

        public AzureRedisSet(string key)
        {
            this.SetKey = key;
            if (db == null)
            {
                lock (locker)
                {
                    db = AzureRedisSet.RedisConnection.GetDatabase();
                }
            }
        }

        /// <summary>
        /// StackExchange Redisclient Method
        /// </summary>
        private static readonly Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            string cacheConnection = Configuration.Config.GetValue<string>(ConfigStrings.RedisConnectionString);
            return ConnectionMultiplexer.Connect(cacheConnection);
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

        public async Task<bool> Add(string value)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await db.SetAddAsync(SetKey, value).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        public async Task<bool> IsMember(string value)
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                return await db.SetContainsAsync(SetKey, value, CommandFlags.None).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        public async Task<bool> Reset()
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                bool done = false;
                try
                {
                    await db.ExecuteAsync("flushdb").ConfigureAwait(true);
                    await db.ExecuteAsync("flushall").ConfigureAwait(true);
                    done = true;
                }
                catch (AggregateException ae)
                {
                    Log.Always(ae.Message);
                }
                return done;
            }).ConfigureAwait(true);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using App.Metrics;
using App.Metrics.Concurrency;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption
{
    public class SessionFactory : IDisposable
    {
        #pragma warning disable SA1401
        protected internal readonly MemoryCache SessionCache;
        #pragma warning restore SA1401

        // Percentage of session cache to compact if it exceeds size limits and remove unused sessions
        private const int CompactionPercentage = 50;

        private static readonly ILogger Logger = LogManager.CreateLogger<SessionFactory>();

        private readonly string productId;
        private readonly string serviceId;
        private readonly IMetastore<JObject> metastore;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache;
        private readonly CryptoPolicy cryptoPolicy;
        private readonly KeyManagementService keyManagementService;
        private readonly ConcurrentDictionary<string, object> semaphoreLocks;

        public SessionFactory(
            string productId,
            string serviceId,
            IMetastore<JObject> metastore,
            SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache,
            CryptoPolicy cryptoPolicy,
            KeyManagementService keyManagementService)
        {
            this.productId = productId;
            this.serviceId = serviceId;
            this.metastore = metastore;
            this.systemKeyCache = systemKeyCache;
            this.cryptoPolicy = cryptoPolicy;
            this.keyManagementService = keyManagementService;
            semaphoreLocks = new ConcurrentDictionary<string, object>();
            SessionCache = new MemoryCache(new MemoryCacheOptions());
        }

        public interface IMetastoreStep
        {
            // Leaving this in here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
            ICryptoPolicyStep WithInMemoryMetastore();

            ICryptoPolicyStep WithMetastore(IMetastore<JObject> metastore);
        }

        public interface ICryptoPolicyStep
        {
            IKeyManagementServiceStep WithNeverExpiredCryptoPolicy();

            IKeyManagementServiceStep WithCryptoPolicy(CryptoPolicy cryptoPolicy);
        }

        public interface IKeyManagementServiceStep
        {
            // Leaving this in here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
            IBuildStep WithStaticKeyManagementService(string demoMasterKey);

            IBuildStep WithKeyManagementService(KeyManagementService keyManagementService);
        }

        public interface IBuildStep
        {
            IBuildStep WithMetrics(IMetrics metrics);

            SessionFactory Build();
        }

        public static IMetastoreStep NewBuilder(string productId, string serviceId)
        {
            return new Builder(productId, serviceId);
        }

        public virtual void Dispose()
        {
            try
            {
                // only close system key cache since we invoke its creation
                systemKeyCache.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "unexpected exception during skCache close");
            }

            // Actually dispose of all the remaining sessions that might be active in the cache.
            lock (SessionCache)
            {
                foreach (KeyValuePair<string, object> sessionCacheKey in semaphoreLocks)
                {
                    CachedSession cachedSession = SessionCache.Get<CachedSession>(sessionCacheKey.Key);

                    // We need to check this to ensure that the entry was not removed by the expiration policy
                    if (cachedSession != null)
                    {
                        // actually close the real thing
                        cachedSession.GetEnvelopeEncryptionJsonImpl().Dispose();
                    }

                    // now remove the entry from the cache
                    SessionCache.Remove(sessionCacheKey.Key);
                }
            }
        }

        public Session<JObject, byte[]> GetSessionJson(string partitionId)
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption = GetEnvelopeEncryptionBytes(partitionId);

            return new SessionJsonImpl<byte[]>(envelopeEncryption);
        }

        public Session<byte[], byte[]> GetSessionBytes(string partitionId)
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption = GetEnvelopeEncryptionBytes(partitionId);

            return new SessionBytesImpl<byte[]>(envelopeEncryption);
        }

        public Session<JObject, JObject> GetSessionJsonAsJson(string partitionId)
        {
            IEnvelopeEncryption<JObject> envelopeEncryption = GetEnvelopeEncryptionJson(partitionId);

            return new SessionJsonImpl<JObject>(envelopeEncryption);
        }

        public Session<byte[], JObject> GetSessionBytesAsJson(string partitionId)
        {
            IEnvelopeEncryption<JObject> envelopeEncryption = GetEnvelopeEncryptionJson(partitionId);

            return new SessionBytesImpl<JObject>(envelopeEncryption);
        }

        internal IEnvelopeEncryption<byte[]> GetEnvelopeEncryptionBytes(string partitionId)
        {
            return new EnvelopeEncryptionBytesImpl(GetEnvelopeEncryptionJson(partitionId));
        }

        internal Partition GetPartition(string partitionId)
        {
            return new Partition(partitionId, serviceId, productId);
        }

        /// <summary>
        /// Atomically acquires a shared <code>CachedSession</code> from the session cache for the
        /// <code>partitionId</code>, creating a new one using the given function if needed. This is used to track the
        /// number of concurrent users so cache eviction policies don't remove an entry while it's still potentially in
        /// use.
        /// </summary>
        ///
        /// <returns>The cached session that's mapped for the given <code>partitionId</code></returns>
        ///
        /// <param name="createSessionFunc">the function to create a new session if there is no current mapping</param>
        /// <param name="partitionId">the partition id for a session</param>
        private CachedSession AcquireShared(
            Func<EnvelopeEncryptionJsonImpl> createSessionFunc, string partitionId)
        {
            object getCachedItemLock = semaphoreLocks.GetOrAdd(partitionId, new object());
            CachedSession cachedItem;

            // TryGetValue is not thread safe and hence we need a lock
            lock (getCachedItemLock)
            {
                if (!SessionCache.TryGetValue(partitionId, out cachedItem))
                {
                    // If the cache size is greater than the maximum count, compact the cache
                    // This will remove all the unused sessions
                    if (SessionCache.Count >= cryptoPolicy.GetSessionCacheMaxSize())
                    {
                        SessionCache.Compact(CompactionPercentage);
                    }

                    // Creating for first time
                    cachedItem = new CachedSession(createSessionFunc(), partitionId, this);
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetPriority(CacheItemPriority.NeverRemove);

                    // Save data in cache.
                    SessionCache.Set(partitionId, cachedItem, cacheEntryOptions);
                }

                // Increment the usage counter of the entry
                cachedItem.IncrementUsageTracker();
            }

            return cachedItem;
        }

        /// <summary>
        /// Atomically marks a shared <code>CachedSession</code> in the session cache as no longer being used by the
        /// current caller for the <code>partitionId</code>. This is used to track the number of concurrent users so
        /// cache eviction policies don't remove an entry while it's still potentially in use.
        /// </summary>
        ///
        /// <param name="partitionId">the partition id for a session</param>
        private void ReleaseShared(string partitionId)
        {
            try
            {
                CachedSession cacheItem = SessionCache.Get<CachedSession>(partitionId);

                // Decrements the usage counter of the entry
                cacheItem.DecrementUsageTracker();

                // If we know it's still in use, don't expire it yet
                if (!cacheItem.IsUsed())
                {
                    // No longer in use, so now kickoff the expire timer
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetPriority(CacheItemPriority.Low)
                        .SetSlidingExpiration(TimeSpan.FromMilliseconds(cryptoPolicy.GetSessionCacheExpireMillis()))
                        .RegisterPostEvictionCallback((id, value, reason, state) =>
                        {
                            ((CachedSession)value).GetEnvelopeEncryptionJsonImpl().Dispose();
                        });
                    SessionCache.Set(partitionId, cacheItem, cacheEntryOptions);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected exception during dispose");
            }
        }

        private IEnvelopeEncryption<JObject> GetEnvelopeEncryptionJson(string partitionId)
        {
            // Wrap the creation logic in a lambda so the cache entry acquisition can create a new instance when needed
            Func<EnvelopeEncryptionJsonImpl> createSessionFunc = () =>
            {
                Partition partition = GetPartition(partitionId);

                return new EnvelopeEncryptionJsonImpl(
                    partition,
                    metastore,
                    systemKeyCache,
                    new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis()),
                    new BouncyAes256GcmCrypto(),
                    cryptoPolicy,
                    keyManagementService);
            };

            if (cryptoPolicy.CanCacheSessions())
            {
                return AcquireShared(createSessionFunc, partitionId);
            }

            return createSessionFunc();
        }

        // Calling it CachedSession but we actually cache the implementing envelope encryption class.
        private class CachedSession : IEnvelopeEncryption<JObject>
        {
            private readonly EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;

            // The usageCounter is used to determine if any callers are still using this instance.
            private readonly StripedLongAdder usageCounter = new StripedLongAdder();
            private readonly string key;
            private readonly SessionFactory sessionFactory;

            public CachedSession(
                EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl,
                string key,
                SessionFactory sessionFactory)
            {
                this.envelopeEncryptionJsonImpl = envelopeEncryptionJsonImpl;
                this.key = key;
                this.sessionFactory = sessionFactory;
            }

            public void Dispose()
            {
                // Instead of closing the session, we call the release function so it can atomically update the usage counter
                sessionFactory.ReleaseShared(key);
            }

            public byte[] DecryptDataRowRecord(JObject dataRowRecord)
            {
                return envelopeEncryptionJsonImpl.DecryptDataRowRecord(dataRowRecord);
            }

            public JObject EncryptPayload(byte[] payload)
            {
                return envelopeEncryptionJsonImpl.EncryptPayload(payload);
            }

            internal void IncrementUsageTracker()
            {
                usageCounter.Increment();
            }

            internal EnvelopeEncryptionJsonImpl GetEnvelopeEncryptionJsonImpl()
            {
                return envelopeEncryptionJsonImpl;
            }

            internal void DecrementUsageTracker()
            {
                usageCounter.Decrement();
            }

            internal bool IsUsed()
            {
                return usageCounter.GetValue() > 0;
            }
        }

        private class Builder : IMetastoreStep, ICryptoPolicyStep, IKeyManagementServiceStep, IBuildStep
        {
            private readonly string productId;
            private readonly string serviceId;

            private IMetastore<JObject> metastore;
            private CryptoPolicy cryptoPolicy;
            private KeyManagementService keyManagementService;
            private IMetrics metrics;

            internal Builder(string productId, string serviceId)
            {
                this.productId = productId;
                this.serviceId = serviceId;
            }

            public ICryptoPolicyStep WithInMemoryMetastore()
            {
                metastore = new InMemoryMetastoreImpl<JObject>();
                return this;
            }

            public ICryptoPolicyStep WithMetastore(IMetastore<JObject> metastore)
            {
                this.metastore = metastore;
                return this;
            }

            public IKeyManagementServiceStep WithNeverExpiredCryptoPolicy()
            {
                cryptoPolicy = new NeverExpiredCryptoPolicy();
                return this;
            }

            public IKeyManagementServiceStep WithCryptoPolicy(CryptoPolicy policy)
            {
                cryptoPolicy = policy;
                return this;
            }

            public IBuildStep WithStaticKeyManagementService(string demoMasterKey)
            {
                keyManagementService = new StaticKeyManagementServiceImpl(demoMasterKey);
                return this;
            }

            public IBuildStep WithKeyManagementService(KeyManagementService kms)
            {
                keyManagementService = kms;
                return this;
            }

            public IBuildStep WithMetrics(IMetrics metrics)
            {
                this.metrics = metrics;
                return this;
            }

            public SessionFactory Build()
            {
                // If no metrics provided, we just create a disabled/no-op one
                if (metrics == null)
                {
                    metrics = new MetricsBuilder()
                        .Configuration.Configure(options => options.Enabled = false)
                        .Build();
                }

                MetricsUtil.SetMetricsInstance(metrics);

                return new SessionFactory(
                    productId,
                    serviceId,
                    metastore,
                    new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis()),
                    cryptoPolicy,
                    keyManagementService);
            }
        }
    }
}

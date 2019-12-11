using System;
using App.Metrics;
using App.Metrics.Concurrency;
using CacheManager.Core;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption
{
    public class SessionFactory : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionFactory>();

        private readonly string productId;
        private readonly string serviceId;
        private readonly IMetastore<JObject> metastore;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache;
        private readonly CryptoPolicy cryptoPolicy;
        private readonly KeyManagementService keyManagementService;
        private readonly ICacheManager<CachedSession> sessionCacheManager;

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
            sessionCacheManager = CacheFactory.Build<CachedSession>(settings => settings
                .WithMicrosoftMemoryCacheHandle("sessionCache"));

            sessionCacheManager.OnRemoveByHandle += (sender, args) =>
            {
                sessionCacheManager.Get(args.Key).GetEnvelopeEncryptionJsonImpl().Dispose();
            };
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

            // TODO : This should force everything to be evicted and process the cleanup
            sessionCacheManager.Clear();
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
        /// Acquires a shared <code>CachedSession</code> from the session cache for the <code>partitionId</code>,
        /// creating a new one if needed using the given function. This is used to track the number of concurrent
        /// users so cache eviction policies don't remove an entry while it's still potentially in use.
        /// </summary>
        ///
        /// <returns>The cached session that's mapped for the given <code>partitionId</code></returns>
        ///
        /// <param name="createSession">the function to create a new session if there is no current mapping</param>
        /// <param name="partitionId">the partition id for a session</param>
        private CachedSession AcquireShared(
            Func<string, CacheItem<CachedSession>> createSession, string partitionId)
        {
            sessionCacheManager.TryGetOrAdd(
                partitionId,
                createSession,
                out CacheItem<CachedSession> cachedItem);

            // Creating for first time and increment usage counter as we're the first user
            cachedItem.Value.IncrementUsageTracker();
            sessionCacheManager.Put(cachedItem.WithNoExpiration());

            return cachedItem.Value;
        }

        private IEnvelopeEncryption<JObject> GetEnvelopeEncryptionJson(string partitionId)
        {
            // Wrap the creation logic in a lambda so the cache entry acquisition can create a new instance when needed
            Func<string, CacheItem<CachedSession>> createFunc = id =>
            {
                Partition partition = GetPartition(partitionId);

                EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl = new EnvelopeEncryptionJsonImpl(
                    partition,
                    metastore,
                    systemKeyCache,
                    new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis()),
                    new BouncyAes256GcmCrypto(),
                    cryptoPolicy,
                    keyManagementService);

                CachedSession cachedSession =
                    new CachedSession(
                        envelopeEncryptionJsonImpl,
                        sessionCacheManager,
                        partitionId,
                        cryptoPolicy.GetSessionCacheExpireMillis());

                return new CacheItem<CachedSession>(partitionId, cachedSession);
            };

            if (cryptoPolicy.CanCacheSessions())
            {
                return AcquireShared(createFunc, partitionId);
            }

            return createFunc.Invoke(partitionId).Value;
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

        private class CachedSession : IEnvelopeEncryption<JObject>
        {
            private readonly EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;

            // The usageCounter is used to determine if any callers are still using this instance.
            private readonly StripedLongAdder usageCounter = new StripedLongAdder();
            private readonly ICacheManager<CachedSession> sessionCacheManager;
            private readonly string key;

            private readonly long sessionCacheExpireMillis;

            public CachedSession(
                EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl,
                ICacheManager<CachedSession> sessionCacheManager,
                string key,
                long sessionCacheExpireMillis)
            {
                this.envelopeEncryptionJsonImpl = envelopeEncryptionJsonImpl;
                this.sessionCacheManager = sessionCacheManager;
                this.key = key;
                this.sessionCacheExpireMillis = sessionCacheExpireMillis;
            }

            public void Dispose()
            {
                // Instead of closing the session, we atomically update the usage counter
                try
                {
                    CacheItem<CachedSession> cacheItem = sessionCacheManager.GetCacheItem(key);
                    cacheItem.Value.DecrementUsageTracker();
                    if (!cacheItem.Value.IsUsed())
                    {
                        // No longer in use, so now kickoff the expire timer
                        sessionCacheManager.Put(
                            cacheItem.WithSlidingExpiration(TimeSpan.FromMilliseconds(sessionCacheExpireMillis)));
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Unexpected exception during dispose");
                }
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

            private void DecrementUsageTracker()
            {
                usageCounter.Decrement();
            }

            private bool IsUsed()
            {
                return usageCounter.GetValue() > 0;
            }
        }
    }
}

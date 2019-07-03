using System;
using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
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
    public class AppEncryptionSessionFactory : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<AppEncryptionSessionFactory>();

        private readonly string productId;
        private readonly string systemId;
        private readonly IMetastorePersistence<JObject> metastorePersistence;
        private readonly SecureCryptoKeyDictionaryFactory<DateTimeOffset> secureCryptoKeyDictionaryFactory;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache;
        private readonly CryptoPolicy cryptoPolicy;
        private readonly KeyManagementService keyManagementService;

        public AppEncryptionSessionFactory(
            string productId,
            string systemId,
            IMetastorePersistence<JObject> metastorePersistence,
            SecureCryptoKeyDictionaryFactory<DateTimeOffset> secureCryptoKeyDictionaryFactory,
            CryptoPolicy cryptoPolicy,
            KeyManagementService keyManagementService)
        {
            this.productId = productId;
            this.systemId = systemId;
            this.metastorePersistence = metastorePersistence;
            this.secureCryptoKeyDictionaryFactory = secureCryptoKeyDictionaryFactory;
            systemKeyCache = secureCryptoKeyDictionaryFactory.CreateSecureCryptoKeyDictionary();
            this.cryptoPolicy = cryptoPolicy;
            this.keyManagementService = keyManagementService;
        }

        public interface IMetastoreStep
        {
            // Leaving this in here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
            ICryptoPolicyStep WithMemoryPersistence();

            ICryptoPolicyStep WithMetaStorePersistence(IMetastorePersistence<JObject> persistence);
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

            AppEncryptionSessionFactory Build();
        }

        public static IMetastoreStep NewBuilder(string productId, string systemId)
        {
            return new Builder(productId, systemId);
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
                Logger.LogError(e, "unexpected exception during close");
            }
        }

        public AppEncryption<JObject, byte[]> GetAppEncryptionJson(string partitionId)
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption = GetEnvelopeEncryptionBytes(partitionId);

            return new AppEncryptionJsonImpl<byte[]>(envelopeEncryption);
        }

        public AppEncryption<byte[], byte[]> GetAppEncryptionBytes(string partitionId)
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption = GetEnvelopeEncryptionBytes(partitionId);

            return new AppEncryptionBytesImpl<byte[]>(envelopeEncryption);
        }

        public AppEncryption<JObject, JObject> GetAppEncryptionJsonAsJson(string partitionId)
        {
            IEnvelopeEncryption<JObject> envelopeEncryption = GetEnvelopeEncryptionJson(partitionId);

            return new AppEncryptionJsonImpl<JObject>(envelopeEncryption);
        }

        public AppEncryption<byte[], JObject> GetAppEncryptionBytesAsJson(string partitionId)
        {
            IEnvelopeEncryption<JObject> envelopeEncryption = GetEnvelopeEncryptionJson(partitionId);

            return new AppEncryptionBytesImpl<JObject>(envelopeEncryption);
        }

        internal IEnvelopeEncryption<byte[]> GetEnvelopeEncryptionBytes(string partitionId)
        {
            return new EnvelopeEncryptionBytesImpl(GetEnvelopeEncryptionJson(partitionId));
        }

        internal AppEncryptionPartition GetAppEncryptionPartition(string partitionId)
        {
            return new AppEncryptionPartition(partitionId, systemId, productId);
        }

        private EnvelopeEncryptionJsonImpl GetEnvelopeEncryptionJson(string partitionId)
        {
            AppEncryptionPartition appEncryptionPartition = GetAppEncryptionPartition(partitionId);

            return new EnvelopeEncryptionJsonImpl(
                appEncryptionPartition,
                metastorePersistence,
                systemKeyCache,
                secureCryptoKeyDictionaryFactory,
                new BouncyAes256GcmCrypto(),
                cryptoPolicy,
                keyManagementService);
        }

        private class Builder : IMetastoreStep, ICryptoPolicyStep, IKeyManagementServiceStep, IBuildStep
        {
            private readonly string productId;
            private readonly string systemId;

            private IMetastorePersistence<JObject> metastorePersistence;
            private CryptoPolicy cryptoPolicy;
            private KeyManagementService keyManagementService;
            private IMetrics metrics;

            internal Builder(string productId, string systemId)
            {
                this.productId = productId;
                this.systemId = systemId;
            }

            public ICryptoPolicyStep WithMemoryPersistence()
            {
                metastorePersistence = new MemoryPersistenceImpl<JObject>();
                return this;
            }

            public ICryptoPolicyStep WithMetaStorePersistence(IMetastorePersistence<JObject> persistence)
            {
                metastorePersistence = persistence;
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

            public AppEncryptionSessionFactory Build()
            {
                // If no metrics provided, we just create a disabled/no-op one
                if (metrics == null)
                {
                    metrics = new MetricsBuilder()
                        .Configuration.Configure(options => options.Enabled = false)
                        .Build();
                }

                MetricsUtil.SetMetricsInstance(metrics);

                return new AppEncryptionSessionFactory(
                    productId,
                    systemId,
                    metastorePersistence,
                    new SecureCryptoKeyDictionaryFactory<DateTimeOffset>(cryptoPolicy),
                    cryptoPolicy,
                    keyManagementService);
            }
        }
    }
}

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
    public class SessionFactory : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionFactory>();

        private readonly string productId;
        private readonly string systemId;
        private readonly IMetastore<JObject> metastore;
        private readonly SecureCryptoKeyDictionaryFactory<DateTimeOffset> secureCryptoKeyDictionaryFactory;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache;
        private readonly CryptoPolicy cryptoPolicy;
        private readonly KeyManagementService keyManagementService;

        public SessionFactory(
            string productId,
            string systemId,
            IMetastore<JObject> metastore,
            SecureCryptoKeyDictionaryFactory<DateTimeOffset> secureCryptoKeyDictionaryFactory,
            CryptoPolicy cryptoPolicy,
            KeyManagementService keyManagementService)
        {
            this.productId = productId;
            this.systemId = systemId;
            this.metastore = metastore;
            this.secureCryptoKeyDictionaryFactory = secureCryptoKeyDictionaryFactory;
            systemKeyCache = secureCryptoKeyDictionaryFactory.CreateSecureCryptoKeyDictionary();
            this.cryptoPolicy = cryptoPolicy;
            this.keyManagementService = keyManagementService;
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
            return new Partition(partitionId, systemId, productId);
        }

        private EnvelopeEncryptionJsonImpl GetEnvelopeEncryptionJson(string partitionId)
        {
            Partition partition = GetPartition(partitionId);

            return new EnvelopeEncryptionJsonImpl(
                partition,
                metastore,
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

            private IMetastore<JObject> metastore;
            private CryptoPolicy cryptoPolicy;
            private KeyManagementService keyManagementService;
            private IMetrics metrics;

            internal Builder(string productId, string systemId)
            {
                this.productId = productId;
                this.systemId = systemId;
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
                    systemId,
                    metastore,
                    new SecureCryptoKeyDictionaryFactory<DateTimeOffset>(cryptoPolicy),
                    cryptoPolicy,
                    keyManagementService);
            }
        }
    }
}

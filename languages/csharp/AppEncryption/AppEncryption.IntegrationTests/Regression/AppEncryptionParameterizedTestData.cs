using System;
using System.Collections.Generic;
using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class AppEncryptionParameterizedTestData
    {
        private static readonly Random Random = new Random();

        public static IEnumerable<object[]> GenerateScenarios()
        {
            foreach (KeyState cacheIK in Enum.GetValues(typeof(KeyState)))
            {
                foreach (KeyState metaIK in Enum.GetValues(typeof(KeyState)))
                {
                    foreach (KeyState cacheSK in Enum.GetValues(typeof(KeyState)))
                    {
                        foreach (KeyState metaSK in Enum.GetValues(typeof(KeyState)))
                        {
                            // TODO Add CryptoPolicy.KeyRotationStrategy loop and update expect/verify logic accordingly
                            yield return GenerateMocks(cacheIK, metaIK, cacheSK, metaSK);
                        }
                    }
                }
            }
        }

         private static object[] GenerateMocks(KeyState cacheIK, KeyState metaIK, KeyState cacheSK, KeyState metaSK)
        {
            AppEncryptionPartition appEncryptionPartition = new AppEncryptionPartition(
                cacheIK + "CacheIK_" + metaIK + "MetaIK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() + "_" + Random.Next(),
                cacheSK + "CacheSK_" + metaSK + "MetaSK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() + "_" + Random.Next(),
                DefaultProductId);

            // TODO Update to create KeyManagementService based on config/param once we plug in AWS KMS
            KeyManagementService kms = new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);

            CryptoKeyHolder cryptoKeyHolder = CryptoKeyHolder.GenerateIKSK();

            // TODO Pass Metastore type to enable spy generation once we plug in external metastore types
            Mock<MemoryPersistenceImpl<JObject>> metastorePersistence = MetastoreMock.CreateMetastoreMock(appEncryptionPartition, kms,  metaIK, metaSK, cryptoKeyHolder);

            CacheMock cacheMock = CacheMock.CreateCacheMock(cacheIK, cacheSK, cryptoKeyHolder);

            // Mimics (mostly) the old TimeBasedCryptoPolicyImpl settings
            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(KeyExpiryDays)
                .WithRevokeCheckMinutes(int.MaxValue)
                .WithCanCacheIntermediateKeys(false)
                .WithCanCacheSystemKeys(false)
                .Build();

            SecureCryptoKeyDictionary<DateTimeOffset> intermediateKeyCache = cacheMock.IntermediateKeyCache;
            SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache = cacheMock.SystemKeyCache;

            EnvelopeEncryptionJsonImpl envelopeEncryptionJson = new EnvelopeEncryptionJsonImpl(
                appEncryptionPartition,
                metastorePersistence.Object,
                systemKeyCache,
                new FakeSecureCryptoKeyDictionaryFactory<DateTimeOffset>(intermediateKeyCache),
                new BouncyAes256GcmCrypto(),
                cryptoPolicy,
                kms);

            IEnvelopeEncryption<byte[]> envelopeEncryptionByteImpl = new EnvelopeEncryptionBytesImpl(envelopeEncryptionJson);

            // Need to manually set a no-op metrics instance
            IMetrics metrics = new MetricsBuilder()
                .Configuration.Configure(options => options.Enabled = false)
                .Build();
            MetricsUtil.SetMetricsInstance(metrics);

            return new object[] { envelopeEncryptionByteImpl, metastorePersistence, cacheIK, metaIK, cacheSK, metaSK, appEncryptionPartition };
        }
    }
}

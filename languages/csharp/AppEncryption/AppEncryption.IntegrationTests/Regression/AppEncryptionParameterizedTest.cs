using System;
using System.Collections;
using System.Collections.Generic;
using App.Metrics;
using GoDaddy.AppServices.AppEncryption.IntegrationTests.Regression;
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
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class AppEncryptionParameterizedTest
    {
        private readonly JObject payload;

        public AppEncryptionParameterizedTest()
        {
            payload = PayloadGenerator.CreateDefaultRandomJsonPayload();
        }

        [Theory]
        [ClassData(typeof(AppEncryptionParameterizedTestData))]
        public void ParameterizedTests(
            IEnvelopeEncryption<byte[]> envelopeEncryptionJson,
            Mock<MemoryPersistenceImpl<JObject>> metastorePersistence,
            KeyState cacheIK,
            KeyState metaIK,
            KeyState cacheSK,
            KeyState metaSK,
            AppEncryptionPartition appEncryptionPartition)
        {
            using (AppEncryption<JObject, byte[]> appEncryptionJsonImpl =
                new AppEncryptionJsonImpl<byte[]>(envelopeEncryptionJson))
            {
                EncryptMetastoreInteractions encryptMetastoreInteractions =
                    new EncryptMetastoreInteractions(cacheIK, metaIK, cacheSK, metaSK);
                DecryptMetastoreInteractions decryptMetastoreInteractions =
                    new DecryptMetastoreInteractions(cacheIK, cacheSK);

                // encrypt with library object(appEncryptionJsonImpl)
                byte[] encryptedPayload = appEncryptionJsonImpl.Encrypt(payload);

                Assert.NotNull(encryptedPayload);
                VerifyEncryptFlow(metastorePersistence, encryptMetastoreInteractions, appEncryptionPartition);

                metastorePersistence.Reset();
                JObject decryptedPayload = appEncryptionJsonImpl.Decrypt(encryptedPayload);

                VerifyDecryptFlow(metastorePersistence, decryptMetastoreInteractions, appEncryptionPartition);
                Assert.True(JToken.DeepEquals(payload, decryptedPayload));
            }
        }

        private void VerifyDecryptFlow(
            Mock<MemoryPersistenceImpl<JObject>> metastorePersistence,
            DecryptMetastoreInteractions metastoreInteractions,
            AppEncryptionPartition appEncryptionPartition)
        {
            // If IK is loaded from metastore
            if (metastoreInteractions.ShouldLoadIK())
            {
                metastorePersistence.Verify(
                    x => x.Load(appEncryptionPartition.IntermediateKeyId, It.IsAny<DateTimeOffset>()), Times.Once);
            }

            // If SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadSK())
            {
                metastorePersistence.Verify(
                    x => x.Load(appEncryptionPartition.SystemKeyId, It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }
        }

        private void VerifyEncryptFlow(
            Mock<MemoryPersistenceImpl<JObject>> metastorePersistence,
            EncryptMetastoreInteractions metastoreInteractions,
            AppEncryptionPartition appEncryptionPartition)
        {
            // If IK is stored to metastore
            if (metastoreInteractions.ShouldStoreIK())
            {
                metastorePersistence.Verify(
                    x => x.Store(appEncryptionPartition.IntermediateKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Once);
            }

            // If SK is stored to metastore
            if (metastoreInteractions.ShouldStoreSK())
            {
                metastorePersistence.Verify(
                    x => x.Store(appEncryptionPartition.SystemKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Once);
            }

            // If neither IK nor SK is stored
            if (!metastoreInteractions.ShouldStoreIK() && !metastoreInteractions.ShouldStoreSK())
            {
                metastorePersistence.Verify(
                    x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Never);
            }

            // NOTE: We do not read IK from the metastore in case of Encrypt
            // If SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadSK())
            {
                metastorePersistence.Verify(
                    x => x.Load(appEncryptionPartition.SystemKeyId, It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }
            else
            {
                metastorePersistence.Verify(
                    x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
                    Times.Never);
            }

            // If latest IK is loaded from metastore
            if (metastoreInteractions.ShouldLoadLatestIK())
            {
                metastorePersistence.Verify(
                    x => x.LoadLatestValue(appEncryptionPartition.IntermediateKeyId),
                    Times.Once);
            }

            // If latest SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadLatestSK())
            {
                metastorePersistence.Verify(
                    x => x.LoadLatestValue(appEncryptionPartition.SystemKeyId),
                    Times.Once);
            }

            // If neither latest IK or SK is loaded from metastore
            if (!metastoreInteractions.ShouldLoadLatestSK() && !metastoreInteractions.ShouldLoadLatestIK())
            {
                metastorePersistence.Verify(
                    x => x.LoadLatestValue(It.IsAny<string>()),
                    Times.Never);
            }
        }

        private class AppEncryptionParameterizedTestData : IEnumerable<object[]>
        {
            private static readonly Random Random = new Random();
            private readonly ConfigFixture configFixture;

            public AppEncryptionParameterizedTestData()
            {
                configFixture = new ConfigFixture();
            }

            public IEnumerator<object[]> GetEnumerator()
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

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private object[] GenerateMocks(KeyState cacheIK, KeyState metaIK, KeyState cacheSK, KeyState metaSK)
            {
                AppEncryptionPartition appEncryptionPartition = new AppEncryptionPartition(
                    cacheIK + "CacheIK_" + metaIK + "MetaIK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() +
                    "_" + Random.Next(),
                    cacheSK + "CacheSK_" + metaSK + "MetaSK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() + "_" + Random.Next(),
                    DefaultProductId);

                KeyManagementService kms = configFixture.KeyManagementService;

                CryptoKeyHolder cryptoKeyHolder = CryptoKeyHolder.GenerateIKSK();

                Mock<MemoryPersistenceImpl<JObject>> metastorePersistence = MetastoreMock.CreateMetastoreMock(
                    appEncryptionPartition, kms, metaIK, metaSK, cryptoKeyHolder);

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

                IEnvelopeEncryption<byte[]> envelopeEncryptionByteImpl =
                    new EnvelopeEncryptionBytesImpl(envelopeEncryptionJson);

                // Need to manually set a no-op metrics instance
                IMetrics metrics = new MetricsBuilder()
                    .Configuration.Configure(options => options.Enabled = false)
                    .Build();
                MetricsUtil.SetMetricsInstance(metrics);

                return new object[]
                {
                    envelopeEncryptionByteImpl, metastorePersistence, cacheIK, metaIK, cacheSK, metaSK,
                    appEncryptionPartition
                };
            }
        }
    }
}

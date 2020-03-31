using System;
using System.Collections;
using System.Collections.Generic;
using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Kms;
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
            Mock<IMetastore<JObject>> metastore,
            KeyState cacheIK,
            KeyState metaIK,
            KeyState cacheSK,
            KeyState metaSK,
            Partition partition)
        {
            using (Session<JObject, byte[]> sessionJsonImpl =
                new SessionJsonImpl<byte[]>(envelopeEncryptionJson))
            {
                EncryptMetastoreInteractions encryptMetastoreInteractions =
                    new EncryptMetastoreInteractions(cacheIK, metaIK, cacheSK, metaSK);
                DecryptMetastoreInteractions decryptMetastoreInteractions =
                    new DecryptMetastoreInteractions(cacheIK, cacheSK);

                // encrypt with library object(sessionJsonImpl)
                byte[] encryptedPayload = sessionJsonImpl.Encrypt(payload);

                Assert.NotNull(encryptedPayload);
                VerifyEncryptFlow(metastore, encryptMetastoreInteractions, partition);

                metastore.Invocations.Clear();
                JObject decryptedPayload = sessionJsonImpl.Decrypt(encryptedPayload);

                VerifyDecryptFlow(metastore, decryptMetastoreInteractions, partition);
                Assert.True(JToken.DeepEquals(payload, decryptedPayload));
            }
        }

        private void VerifyDecryptFlow(
            Mock<IMetastore<JObject>> metastore,
            DecryptMetastoreInteractions metastoreInteractions,
            Partition partition)
        {
            // If IK is loaded from metastore
            if (metastoreInteractions.ShouldLoadIK())
            {
                metastore.Verify(
                    x => x.Load(partition.IntermediateKeyId, It.IsAny<DateTimeOffset>()), Times.Once);
            }

            // If SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadSK())
            {
                metastore.Verify(
                    x => x.Load(partition.SystemKeyId, It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }
        }

        private void VerifyEncryptFlow(
            Mock<IMetastore<JObject>> metastore,
            EncryptMetastoreInteractions metastoreInteractions,
            Partition partition)
        {
            // If IK is stored to metastore
            if (metastoreInteractions.ShouldStoreIK())
            {
                metastore.Verify(
                    x => x.Store(partition.IntermediateKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Once);
            }
            else
            {
                metastore.Verify(
                    x => x.Store(partition.IntermediateKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Never);
            }

            // If SK is stored to metastore
            if (metastoreInteractions.ShouldStoreSK())
            {
                metastore.Verify(
                    x => x.Store(partition.SystemKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Once);
            }
            else
            {
                metastore.Verify(
                    x => x.Store(partition.SystemKeyId, It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Never);
            }

            // If neither IK nor SK is stored
            if (!metastoreInteractions.ShouldStoreIK() && !metastoreInteractions.ShouldStoreSK())
            {
                metastore.Verify(
                    x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()),
                    Times.Never);
            }

            // NOTE: We do not read IK from the metastore in case of Encrypt
            // If SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadSK())
            {
                metastore.Verify(
                    x => x.Load(partition.SystemKeyId, It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }
            else
            {
                metastore.Verify(
                    x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
                    Times.Never);
            }

            // If latest IK is loaded from metastore
            if (metastoreInteractions.ShouldLoadLatestIK())
            {
                metastore.Verify(x => x.LoadLatest(partition.IntermediateKeyId), Times.Once);
            }
            else
            {
                metastore.Verify(x => x.LoadLatest(partition.IntermediateKeyId), Times.Never);
            }

            // If latest SK is loaded from metastore
            if (metastoreInteractions.ShouldLoadLatestSK())
            {
                metastore.Verify(x => x.LoadLatest(partition.SystemKeyId), Times.Once);
            }
            else
            {
                metastore.Verify(x => x.LoadLatest(partition.SystemKeyId), Times.Never);
            }

            // If neither latest IK or SK is loaded from metastore
            if (!metastoreInteractions.ShouldLoadLatestSK() && !metastoreInteractions.ShouldLoadLatestIK())
            {
                metastore.Verify(
                    x => x.LoadLatest(It.IsAny<string>()),
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

                // We do not log metrics for the Parameterized tests but we still need to set a disabled/no-op metrics
                // instance for the tests to run successfully. This is done below.
                IMetricsRoot metrics = new MetricsBuilder()
                    .Configuration.Configure(options => options.Enabled = false)
                    .Build();
                MetricsUtil.SetMetricsInstance(metrics);
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
                Partition partition = new Partition(
                    cacheIK + "CacheIK_" + metaIK + "MetaIK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() +
                    "_" + Random.Next(),
                    cacheSK + "CacheSK_" + metaSK + "MetaSK_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset() + "_" + Random.Next(),
                    DefaultProductId);

                KeyManagementService kms = configFixture.KeyManagementService;

                CryptoKeyHolder cryptoKeyHolder = CryptoKeyHolder.GenerateIKSK();

                Mock<IMetastore<JObject>> metastoreMock = MetastoreMock.CreateMetastoreMock(
                    partition, kms, metaIK, metaSK, cryptoKeyHolder, configFixture.Metastore);

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
                    partition,
                    metastoreMock.Object,
                    systemKeyCache,
                    intermediateKeyCache,
                    new BouncyAes256GcmCrypto(),
                    cryptoPolicy,
                    kms);

                IEnvelopeEncryption<byte[]> envelopeEncryptionByteImpl =
                    new EnvelopeEncryptionBytesImpl(envelopeEncryptionJson);

                return new object[]
                {
                    envelopeEncryptionByteImpl, metastoreMock, cacheIK, metaIK, cacheSK, metaSK,
                    partition,
                };
            }
        }
    }
}

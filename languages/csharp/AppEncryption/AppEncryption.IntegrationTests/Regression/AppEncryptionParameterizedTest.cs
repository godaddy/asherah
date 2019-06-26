using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

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
        [MemberData(nameof(AppEncryptionParameterizedTestData.GenerateScenarios), MemberType = typeof(AppEncryptionParameterizedTestData))]
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
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Xunit;
using Xunit.Sdk;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    [Collection("Configuration collection")]
    public class AppEncryptionBytesTest : IDisposable
    {
        private static readonly Persistence<byte[]> PersistenceBytes = PersistenceFactory<byte[]>.CreateInMemoryPersistence();
        private readonly byte[] payload;
        private readonly AppEncryptionSessionFactory appEncryptionSessionFactory;
        private readonly string partitionId;
        private readonly AppEncryption<byte[], byte[]> appEncryptionBytes;

        public AppEncryptionBytesTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            appEncryptionSessionFactory = SessionFactoryGenerator.CreateDefaultAppEncryptionSessionFactory(
                configFixture.KeyManagementService,
                configFixture.MetastorePersistence);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            appEncryptionBytes = appEncryptionSessionFactory.GetAppEncryptionBytes(partitionId);
        }

        public void Dispose()
        {
            appEncryptionBytes.Dispose();
            appEncryptionSessionFactory.Dispose();
        }

        [Fact]
        private void BytesEncryptDecrypt()
        {
            byte[] dataRowRecord = appEncryptionBytes.Encrypt(payload);
            byte[] decryptedPayload = appEncryptionBytes.Decrypt(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void BytesEncryptDecryptSameSessionMultipleRounds()
        {
            // Just loop a bunch of times to verify no surprises
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                byte[] dataRowRecord = appEncryptionBytes.Encrypt(payload);
                byte[] decryptedPayload = appEncryptionBytes.Decrypt(dataRowRecord);

                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void BytesStoreLoad()
        {
            string persistenceKey = appEncryptionBytes.Store(payload, PersistenceBytes);

            Option<byte[]> decryptedPayload = appEncryptionBytes.Load(persistenceKey, PersistenceBytes);

            if (decryptedPayload.IsSome)
            {
                Assert.Equal(payload, (byte[])decryptedPayload);
            }
            else
            {
                throw new XunitException("Byte load did not return decrypted payload");
            }
        }

        [Fact]
        private void BytesLoadInvalidKey()
        {
            string persistenceKey = "1234";

            Option<byte[]> decryptedPayload = appEncryptionBytes.Load(persistenceKey, PersistenceBytes);

            Assert.False(decryptedPayload.IsSome);
        }

        [Fact]
        private void BytesEncryptDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = appEncryptionBytes.Encrypt(payload);

            using (AppEncryption<byte[], byte[]> appEncryptionBytesNew = appEncryptionSessionFactory.GetAppEncryptionBytes(partitionId))
            {
                byte[] decryptedPayload = appEncryptionBytesNew.Decrypt(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void BytesEncryptDecryptWithDifferentPayloads()
        {
            byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecord1 = appEncryptionBytes.Encrypt(payload);
            byte[] dataRowRecord2 = appEncryptionBytes.Encrypt(otherPayload);

            byte[] decryptedPayload1 = appEncryptionBytes.Decrypt(dataRowRecord1);
            byte[] decryptedPayload2 = appEncryptionBytes.Decrypt(dataRowRecord2);

            Assert.Equal(payload, decryptedPayload1);
            Assert.Equal(otherPayload, decryptedPayload2);
        }

        [Fact]
        private void BytesStoreOverwritePayload()
        {
            string key = "some_key";
            byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();

            appEncryptionBytes.Store(key, payload, PersistenceBytes);
            appEncryptionBytes.Store(key, otherPayload, PersistenceBytes);
            Option<byte[]> decryptedPayload = appEncryptionBytes.Load(key, PersistenceBytes);

            Assert.Equal(otherPayload, (byte[])decryptedPayload);
        }
    }
}

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
    public class SessionBytesTest : IDisposable
    {
        private static readonly Persistence<byte[]> PersistenceBytes = PersistenceFactory<byte[]>.CreateInMemoryPersistence();
        private readonly byte[] payload;
        private readonly SessionFactory sessionFactory;
        private readonly string partitionId;
        private readonly Session<byte[], byte[]> sessionBytes;

        public SessionBytesTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService,
                configFixture.Metastore);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            sessionBytes = sessionFactory.GetSessionBytes(partitionId);
        }

        public void Dispose()
        {
            sessionBytes.Dispose();
            sessionFactory.Dispose();
        }

        [Fact]
        private void BytesEncryptDecrypt()
        {
            byte[] dataRowRecord = sessionBytes.Encrypt(payload);
            byte[] decryptedPayload = sessionBytes.Decrypt(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void BytesEncryptDecryptSameSessionMultipleRounds()
        {
            // Just loop a bunch of times to verify no surprises
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                byte[] dataRowRecord = sessionBytes.Encrypt(payload);
                byte[] decryptedPayload = sessionBytes.Decrypt(dataRowRecord);

                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void BytesStoreLoad()
        {
            string persistenceKey = sessionBytes.Store(payload, PersistenceBytes);

            Option<byte[]> decryptedPayload = sessionBytes.Load(persistenceKey, PersistenceBytes);

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

            Option<byte[]> decryptedPayload = sessionBytes.Load(persistenceKey, PersistenceBytes);

            Assert.False(decryptedPayload.IsSome);
        }

        [Fact]
        private void BytesEncryptDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = sessionBytes.Encrypt(payload);

            using (Session<byte[], byte[]> sessionBytesNew = sessionFactory.GetSessionBytes(partitionId))
            {
                byte[] decryptedPayload = sessionBytesNew.Decrypt(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void BytesEncryptDecryptWithDifferentPayloads()
        {
            byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecord1 = sessionBytes.Encrypt(payload);
            byte[] dataRowRecord2 = sessionBytes.Encrypt(otherPayload);

            byte[] decryptedPayload1 = sessionBytes.Decrypt(dataRowRecord1);
            byte[] decryptedPayload2 = sessionBytes.Decrypt(dataRowRecord2);

            Assert.Equal(payload, decryptedPayload1);
            Assert.Equal(otherPayload, decryptedPayload2);
        }

        [Fact]
        private void BytesStoreOverwritePayload()
        {
            string key = "some_key";
            byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();

            sessionBytes.Store(key, payload, PersistenceBytes);
            sessionBytes.Store(key, otherPayload, PersistenceBytes);
            Option<byte[]> decryptedPayload = sessionBytes.Load(key, PersistenceBytes);

            Assert.Equal(otherPayload, (byte[])decryptedPayload);
        }
    }
}

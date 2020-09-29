using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Sdk;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    [Collection("Configuration collection")]
    public class SessionBytesTest
    {
        private static readonly Persistence<byte[]> PersistenceBytes = PersistenceFactory<byte[]>.CreateInMemoryPersistence();
        private readonly byte[] payload;
        private readonly string partitionId;
        private readonly ConfigFixture configFixture;

        public SessionBytesTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            this.configFixture = configFixture;
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesEncryptDecrypt(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
                {
                    byte[] dataRowRecord = sessionBytes.Encrypt(payload);
                    byte[] decryptedPayload = sessionBytes.Decrypt(dataRowRecord);

                    Assert.Equal(payload, decryptedPayload);
                }
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesEncryptDecryptSameSessionMultipleRounds(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
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
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesStoreLoad(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
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
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesLoadInvalidKey(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
                {
                    string persistenceKey = "1234";

                    Option<byte[]> decryptedPayload = sessionBytes.Load(persistenceKey, PersistenceBytes);

                    Assert.False(decryptedPayload.IsSome);
                }
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesEncryptDecryptWithDifferentSession(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
                {
                    byte[] dataRowRecord = sessionBytes.Encrypt(payload);

                    using (Session<byte[], byte[]> sessionBytesNew = sessionFactory.GetSessionBytes(partitionId))
                    {
                        byte[] decryptedPayload = sessionBytesNew.Decrypt(dataRowRecord);
                        Assert.Equal(payload, decryptedPayload);
                    }
                }
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesEncryptDecryptWithDifferentPayloads(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
                {
                    byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
                    byte[] dataRowRecord1 = sessionBytes.Encrypt(payload);
                    byte[] dataRowRecord2 = sessionBytes.Encrypt(otherPayload);

                    byte[] decryptedPayload1 = sessionBytes.Decrypt(dataRowRecord1);
                    byte[] decryptedPayload2 = sessionBytes.Decrypt(dataRowRecord2);

                    Assert.Equal(payload, decryptedPayload1);
                    Assert.Equal(otherPayload, decryptedPayload2);
                }
            }
        }

        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void BytesStoreOverwritePayload(IConfiguration configuration)
        {
            using (var sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.Metastore,
                configuration))
            {
                using (var sessionBytes = sessionFactory.GetSessionBytes(partitionId))
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
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    [Collection("Configuration collection")]
    public class SessionJsonTest : IDisposable
    {
        private static readonly Persistence<byte[]> PersistenceBytes =
            PersistenceFactory<byte[]>.CreateInMemoryPersistence();

        private readonly JObject payload;
        private readonly SessionFactory sessionFactory;
        private readonly string partitionId;
        private readonly Session<JObject, byte[]> sessionJson;

        public SessionJsonTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomJsonPayload();
            sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService,
                configFixture.Metastore);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            sessionJson = sessionFactory.GetSessionJson(partitionId);
        }

        public void Dispose()
        {
            sessionJson.Dispose();
            sessionFactory.Dispose();
        }

        [Fact]
        private void JsonEncryptDecrypt()
        {
            byte[] dataRowRecord = sessionJson.Encrypt(payload);
            JObject decryptedPayload = sessionJson.Decrypt(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void JsonEncryptDecryptSameSessionMultipleRounds()
        {
            // Just loop a bunch of times to verify no surprises
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                byte[] dataRowRecord = sessionJson.Encrypt(payload);
                JObject decryptedPayload = sessionJson.Decrypt(dataRowRecord);

                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void JsonStoreLoad()
        {
            string persistenceKey = sessionJson.Store(payload, PersistenceBytes);

            Option<JObject> decryptedPayload = sessionJson.Load(persistenceKey, PersistenceBytes);

            if (decryptedPayload.IsSome)
            {
                Assert.Equal(payload, (JObject)decryptedPayload);
            }
            else
            {
                throw new XunitException("Json load did not return decrypted payload");
            }
        }

        [Fact]
        private void JsonLoadInvalidKey()
        {
            string persistenceKey = "1234";

            Option<JObject> decryptedPayload = sessionJson.Load(persistenceKey, PersistenceBytes);

            Assert.False(decryptedPayload.IsSome);
        }

        [Fact]
        private void JsonEncryptDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = sessionJson.Encrypt(payload);

            using (Session<JObject, byte[]> sessionBytesNew =
                sessionFactory.GetSessionJson(partitionId))
            {
                JObject decryptedPayload = sessionBytesNew.Decrypt(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void JsonEncryptDecryptWithDifferentPayloads()
        {
            JObject otherPayload = PayloadGenerator.CreateDefaultRandomJsonPayload();
            byte[] dataRowRecord1 = sessionJson.Encrypt(payload);
            byte[] dataRowRecord2 = sessionJson.Encrypt(otherPayload);

            JObject decryptedPayload1 = sessionJson.Decrypt(dataRowRecord1);
            JObject decryptedPayload2 = sessionJson.Decrypt(dataRowRecord2);

            Assert.Equal(payload, decryptedPayload1);
            Assert.Equal(otherPayload, decryptedPayload2);
        }

        [Fact]
        private void JsonStoreOverwritePayload()
        {
            string key = "some_key";
            JObject otherPayload = PayloadGenerator.CreateDefaultRandomJsonPayload();

            sessionJson.Store(key, payload, PersistenceBytes);
            sessionJson.Store(key, otherPayload, PersistenceBytes);
            Option<JObject> decryptedPayload = sessionJson.Load(key, PersistenceBytes);

            Assert.Equal(otherPayload, (JObject)decryptedPayload);
        }
    }
}

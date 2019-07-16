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
    public class AppEncryptionJsonTest : IDisposable
    {
        private static readonly Persistence<byte[]> PersistenceBytes =
            PersistenceFactory<byte[]>.CreateInMemoryPersistence();

        private readonly JObject payload;
        private readonly AppEncryptionSessionFactory appEncryptionSessionFactory;
        private readonly string partitionId;
        private readonly AppEncryption<JObject, byte[]> appEncryptionJson;

        public AppEncryptionJsonTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomJsonPayload();
            appEncryptionSessionFactory = SessionFactoryGenerator.CreateDefaultAppEncryptionSessionFactory(configFixture.KeyManagementService, configFixture.MetastorePersistence);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            appEncryptionJson = appEncryptionSessionFactory.GetAppEncryptionJson(partitionId);
        }

        public void Dispose()
        {
            appEncryptionJson.Dispose();
            appEncryptionSessionFactory.Dispose();
        }

        [Fact]
        private void JsonEncryptDecrypt()
        {
            byte[] dataRowRecord = appEncryptionJson.Encrypt(payload);
            JObject decryptedPayload = appEncryptionJson.Decrypt(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void JsonEncryptDecryptSameSessionMultipleRounds()
        {
            // Just loop a bunch of times to verify no surprises
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                byte[] dataRowRecord = appEncryptionJson.Encrypt(payload);
                JObject decryptedPayload = appEncryptionJson.Decrypt(dataRowRecord);

                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void JsonStoreLoad()
        {
            string persistenceKey = appEncryptionJson.Store(payload, PersistenceBytes);

            Option<JObject> decryptedPayload = appEncryptionJson.Load(persistenceKey, PersistenceBytes);

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

            Option<JObject> decryptedPayload = appEncryptionJson.Load(persistenceKey, PersistenceBytes);

            Assert.False(decryptedPayload.IsSome);
        }

        [Fact]
        private void JsonEncryptDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = appEncryptionJson.Encrypt(payload);

            using (AppEncryption<JObject, byte[]> appEncryptionBytesNew =
                appEncryptionSessionFactory.GetAppEncryptionJson(partitionId))
            {
                JObject decryptedPayload = appEncryptionBytesNew.Decrypt(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void JsonEncryptDecryptWithDifferentPayloads()
        {
            JObject otherPayload = PayloadGenerator.CreateDefaultRandomJsonPayload();
            byte[] dataRowRecord1 = appEncryptionJson.Encrypt(payload);
            byte[] dataRowRecord2 = appEncryptionJson.Encrypt(otherPayload);

            JObject decryptedPayload1 = appEncryptionJson.Decrypt(dataRowRecord1);
            JObject decryptedPayload2 = appEncryptionJson.Decrypt(dataRowRecord2);

            Assert.Equal(payload, decryptedPayload1);
            Assert.Equal(otherPayload, decryptedPayload2);
        }

        [Fact]
        private void JsonStoreOverwritePayload()
        {
            string key = "some_key";
            JObject otherPayload = PayloadGenerator.CreateDefaultRandomJsonPayload();

            appEncryptionJson.Store(key, payload, PersistenceBytes);
            appEncryptionJson.Store(key, otherPayload, PersistenceBytes);
            Option<JObject> decryptedPayload = appEncryptionJson.Load(key, PersistenceBytes);

            Assert.Equal(otherPayload, (JObject)decryptedPayload);
        }
    }
}

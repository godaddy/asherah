using System;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using Xunit;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression.Core
{
    [Collection("Configuration collection")]
    public class EncryptionSessionTest : IDisposable
    {
        private readonly byte[] payload;
        private readonly GoDaddy.Asherah.AppEncryption.Core.SessionFactory sessionFactory;
        private readonly string partitionId;
        private readonly IEncryptionSession encryptionSession;

        public EncryptionSessionTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            sessionFactory = CoreSessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            encryptionSession = sessionFactory.GetSession(partitionId);
        }

        public void Dispose()
        {
            encryptionSession?.Dispose();
            sessionFactory?.Dispose();
        }

        [Fact]
        private void EncryptDecrypt()
        {
            byte[] dataRowRecord = encryptionSession.Encrypt(payload);
            byte[] decryptedPayload = encryptionSession.Decrypt(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void EncryptDecryptSameSessionMultipleRounds()
        {
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                byte[] dataRowRecord = encryptionSession.Encrypt(payload);
                byte[] decryptedPayload = encryptionSession.Decrypt(dataRowRecord);

                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void EncryptDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = encryptionSession.Encrypt(payload);

            using (IEncryptionSession otherSession = sessionFactory.GetSession(partitionId))
            {
                byte[] decryptedPayload = otherSession.Decrypt(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }

        [Fact]
        private void EncryptDecryptWithDifferentPayloads()
        {
            byte[] otherPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecord1 = encryptionSession.Encrypt(payload);
            byte[] dataRowRecord2 = encryptionSession.Encrypt(otherPayload);

            byte[] decryptedPayload1 = encryptionSession.Decrypt(dataRowRecord1);
            byte[] decryptedPayload2 = encryptionSession.Decrypt(dataRowRecord2);

            Assert.Equal(payload, decryptedPayload1);
            Assert.Equal(otherPayload, decryptedPayload2);
        }

        [Fact]
        private async Task EncryptAsyncDecryptAsync()
        {
            byte[] dataRowRecord = await encryptionSession.EncryptAsync(payload);
            byte[] decryptedPayload = await encryptionSession.DecryptAsync(dataRowRecord);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private async Task EncryptAsyncDecryptWithDifferentSession()
        {
            byte[] dataRowRecord = await encryptionSession.EncryptAsync(payload);

            using (IEncryptionSession otherSession = sessionFactory.GetSession(partitionId))
            {
                byte[] decryptedPayload = await otherSession.DecryptAsync(dataRowRecord);
                Assert.Equal(payload, decryptedPayload);
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using MySql.Data.MySqlClient;
using TechTalk.SpecFlow;
using Xunit;

using static GoDaddy.Asherah.CrossLanguage.CSharp.Constants;

namespace GoDaddy.Asherah.CrossLanguage.CSharp
{
    [Binding]
    public class DecryptDefinitions
    {
        private byte[] encryptedPayload;
        private string decryptedPayload;

        [Given(@"I have encrypted_data from ""(.*)""")]
        public void GivenIHaveEncrypted_DataFrom(string fileName)
        {
            // Read the encrypted payload from the provided file
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", FileDirectory);
            string payload = File.ReadAllText(filePath + "/" + fileName);

            encryptedPayload = Convert.FromBase64String(payload);
        }

        [When(@"I decrypt the encrypted_data")]
        public void WhenIDecryptTheEncrypted_Data()
        {
            KeyManagementService keyManagementService =
                new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);

            AdoMetastoreImpl metastore = AdoMetastoreImpl
                .NewBuilder(MySqlClientFactory.Instance, AdoConnectionString)
                .Build();

            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
                .NewBuilder()
                .WithKeyExpirationDays(KeyExpiryDays)
                .WithRevokeCheckMinutes(RevokeCheckMinutes)
                .Build();

            // Create a session factory for the test
            using (SessionFactory sessionFactory = SessionFactory
                .NewBuilder(DefaultProductId, DefaultServiceId)
                .WithMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .Build())
            {
                // Now create an actual session for a partition (which in our case is a dummy id). This session is used
                // for a transaction and is disposed automatically after use due to the IDisposable implementation.
                using (Session<byte[], byte[]> sessionBytes =
                    sessionFactory.GetSessionBytes(DefaultPartitionId))
                {
                    decryptedPayload = Encoding.UTF8.GetString(sessionBytes.Decrypt(encryptedPayload));
                }
            }
        }

        [Then(@"I get should get decrypted_data")]
        public void ThenIGetShouldGetDecrypted_Data()
        {
            // No action required here since decrypted payload is calculated in the WHEN step
        }

        [Then(@"decrypted_data should be equal to ""(.*)""")]
        public void ThenDecrypted_DataShouldBeEqualTo(string originalPayload)
        {
            Assert.Equal(originalPayload, decryptedPayload);
        }
    }
}

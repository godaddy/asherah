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

using static GoDaddy.Asherah.Cltf.Constants;

namespace GoDaddy.Asherah.Cltf
{
    [Binding]
    public class EncryptDefinitions
    {
        private string payloadString;
        private string encryptedPayloadString;
        private byte[] encryptedBytes;

        [Given(@"I have ""(.*)""")]
        public void IHave(string payload)
        {
            payloadString = payload;
        }

        [When(@"I encrypt the data")]
        public void IEncryptTheData()
        {
            KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);

            AdoMetastoreImpl metastore = AdoMetastoreImpl
                .NewBuilder(MySqlClientFactory.Instance, AdoConnectionString)
                .Build();

            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
                .NewBuilder()
                .WithKeyExpirationDays(KeyExpiryDays)
                .WithRevokeCheckMinutes(RevokeCheckMinutes)
                .Build();

            // Create a session for this test
            using (SessionFactory sessionFactory = SessionFactory
                .NewBuilder(DefaultProductId, DefaultServiceId)
                .WithMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .Build())
            {
                // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
                // for a transaction and is disposed automatically after use due to the IDisposable implementation.
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes(DefaultPartitionId))
                {
                    encryptedBytes = sessionBytes.Encrypt(Encoding.UTF8.GetBytes(payloadString));
                    encryptedPayloadString = Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        [Then(@"I should get encrypted_data")]
        public void IGetShouldGetEncrypted_Data()
        {
            // Write the encrypted payload to a file so that we can decrypt later
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", FileDirectory);
            File.WriteAllText(filePath + "/" + FileName, encryptedPayloadString);
        }

        [Then(@"encrypted_data should not be equal to data")]
        public void Encrypted_DataShouldNotEqualData()
        {
            Assert.NotEqual(payloadString, encryptedPayloadString);
        }
    }
}

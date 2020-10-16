using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;
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

        [Given(@"I have ""(.*)""")]
        public void IHave(string payload)
        {
            payloadString = payload;
        }

        [When(@"I encrypt the data")]
        public void IEncryptTheData()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "test", "true" },
            }).Build();

            AdoMetastoreImpl metastore = AdoMetastoreImpl
                .NewBuilder(MySqlClientFactory.Instance, AdoConnectionString)
                .Build();

            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
                .NewBuilder()
                .WithKeyExpirationDays(KeyExpiryDays)
                .WithRevokeCheckMinutes(RevokeCheckMinutes)
                .Build();

            KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey, cryptoPolicy, configuration);

            // Create a session for this test
            using (SessionFactory sessionFactory = SessionFactory
                .NewBuilder(DefaultProductId, DefaultServiceId)
                .WithConfiguration(configuration)
                .WithMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .Build())
            {
                // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
                // for a transaction and is disposed automatically after use due to the IDisposable implementation.
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes(DefaultPartitionId))
                {
                    byte[] encryptedBytes = sessionBytes.Encrypt(Encoding.UTF8.GetBytes(payloadString));
                    encryptedPayloadString = Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        [Then(@"I should get encrypted_data")]
        public void IShouldGetEncrypted_Data()
        {
            string tempFile = FileDirectory + FileName;

            // Delete any existing encrypted payload file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            // Write the encrypted payload to a file so that we can decrypt later
            File.WriteAllText(FileDirectory + FileName, encryptedPayloadString);
        }

        [Then(@"encrypted_data should not be equal to data")]
        public void Encrypted_DataShouldNotBeEqualToData()
        {
            Assert.NotEqual(payloadString, encryptedPayloadString);
        }
    }
}

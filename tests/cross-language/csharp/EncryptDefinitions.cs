using System;
using System.IO;
using System.Reflection;
using System.Text;
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using MySql.Data.MySqlClient;
using TechTalk.SpecFlow;
using Xunit;
using Xunit.Sdk;

namespace Csharp
{
    [Binding]
    public class EncryptDefinitions
    {
        private string payloadString;
        private string encryptedPayloadString;
        private byte[] encryptedBytes;

        [Given(@"I have ""(.*)""")]
        public void GivenIHave(string payload)
        {
            payloadString = payload;
        }

        [When(@"I encrypt the data")]
        public void WhenIEncryptTheData()
        {
            KeyManagementService keyManagementService =
                new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!");
            AdoMetastoreImpl metastore = AdoMetastoreImpl
                .NewBuilder(
                    MySqlClientFactory.Instance,
                    "server=localhost;uid=root;pwd=Password123;sslmode=none;Initial Catalog=test")
                .Build();
            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
                .NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(60)
                .Build();
            using (SessionFactory sessionFactory = SessionFactory
                .NewBuilder("productId", "reference_app")
                .WithMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .Build())
            {
                // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
                // for a transaction and is disposed automatically after use due to the IDisposable implementation.
                using (Session<byte[], byte[]> sessionBytes =
                    sessionFactory.GetSessionBytes("shopper123"))
                {
                    encryptedBytes = sessionBytes.Encrypt(Encoding.UTF8.GetBytes(payloadString));
                    encryptedPayloadString = Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        [Then(@"I get should get encrypted_data")]
        public void ThenIGetShouldGetEncrypted_Data()
        {
            var path = Directory.GetParent(Directory.GetCurrentDirectory());
            File.WriteAllText(path + "/csharp_encrypted", encryptedPayloadString);
        }

        [Then(@"encrypted_data should not equal data")]
        public void ThenEncrypted_DataShouldNotEqualData()
        {
            Assert.NotEqual(payloadString,  encryptedPayloadString);
        }
    }
}
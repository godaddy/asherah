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

namespace Csharp
{
    [Binding]
    public class DecryptDefinitions
    {
        private byte[] encryptedPayload;
        private string decryptedPayload;

        [Given(@"I have encrypted_data")]
        public void GivenIHaveEncrypted_Data()
        {
            string payload = File.ReadAllText("csharp_encrypted");
            encryptedPayload = Convert.FromBase64String(payload);
        }

        [When(@"I decrypt the encrypted_data")]
        public void WhenIDecryptTheEncrypted_Data()
        {
            KeyManagementService keyManagementService =
                new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!");
            AdoMetastoreImpl metastore = AdoMetastoreImpl
                .NewBuilder(
                    MySqlClientFactory.Instance,
                    "server=127.0.0.1;uid=root;pwd=Password123;sslmode=none;Initial Catalog=test")
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
                    decryptedPayload = Encoding.UTF8.GetString(sessionBytes.Decrypt(encryptedPayload));
                }
            }
        }

        [Then(@"I get should get decrypted_data")]
        public void ThenIGetShouldGetDecrypted_Data()
        {
        }

        [Then(@"decrypted_data should be equal to ""(.*)""")]
        public void ThenDecrypted_DataShouldBeEqualTo(string originalPayload)
        {
            Assert.Equal(originalPayload, decryptedPayload);
        }
    }
}

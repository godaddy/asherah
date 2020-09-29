using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        private const string TestStaticMasterKey = "thisIsAStaticMasterKeyForTesting";

        public static SessionFactory CreateDefaultSessionFactory(IConfiguration configuration)
        {
            var metastore = MetastoreSelector<JObject>.SelectMetastoreWithConfiguration(configuration);
            var cryptoPolicy = BasicExpiringCryptoPolicy.BuildWithConfiguration(configuration);
            var keyManagementService = new StaticKeyManagementServiceImpl(TestStaticMasterKey, cryptoPolicy, configuration);
            return CreateDefaultSessionFactory(DefaultProductId, DefaultServiceId, keyManagementService, cryptoPolicy, metastore, configuration);
        }

        public static SessionFactory CreateDefaultSessionFactory(IMetastore<JObject> metastore, IConfiguration configuration)
        {
            var cryptoPolicy = BasicExpiringCryptoPolicy.BuildWithConfiguration(configuration);
            var keyManagementService = new StaticKeyManagementServiceImpl(TestStaticMasterKey, cryptoPolicy, configuration);
            return CreateDefaultSessionFactory(DefaultProductId, DefaultServiceId, keyManagementService, cryptoPolicy, metastore, configuration);
        }

        private static SessionFactory CreateDefaultSessionFactory(
            string productId,
            string serviceId,
            KeyManagementService keyManagementService,
            CryptoPolicy cryptoPolicy,
            IMetastore<JObject> metastore,
            IConfiguration configuration)
        {
            return SessionFactory.NewBuilder(productId, serviceId)
                .WithConfiguration(configuration)
                .WithMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .Build();
        }
    }
}

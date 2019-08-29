using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        public static SessionFactory CreateDefaultSessionFactory(
            KeyManagementService keyManagementService, IMetastore<JObject> metastore)
        {
            return CreateDefaultSessionFactory(DefaultProductId, DefaultSystemId, keyManagementService, metastore);
        }

        private static SessionFactory CreateDefaultSessionFactory(
            string productId,
            string systemId,
            KeyManagementService keyManagementService,
            IMetastore<JObject> metastore)
        {
            return SessionFactory.NewBuilder(productId, systemId)
                .WithMetastore(metastore)
                .WithNeverExpiredCryptoPolicy()
                .WithKeyManagementService(keyManagementService)
                .Build();
        }
    }
}

using GoDaddy.Asherah.AppEncryption.Kms;
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
            return CreateDefaultSessionFactory(DefaultProductId, DefaultServiceId, keyManagementService, metastore);
        }

        private static SessionFactory CreateDefaultSessionFactory(
            string productId,
            string serviceId,
            KeyManagementService keyManagementService,
            IMetastore<JObject> metastore)
        {
            return SessionFactory.NewBuilder(productId, serviceId)
                .WithMetastore(metastore)
                .WithNeverExpiredCryptoPolicy()
                .WithKeyManagementService(keyManagementService)
                .Build();
        }
    }
}

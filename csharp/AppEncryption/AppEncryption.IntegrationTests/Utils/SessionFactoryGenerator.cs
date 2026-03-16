using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        public static SessionFactory CreateDefaultSessionFactory(
            IKeyManagementService keyManagementService, IMetastore<JObject> metastore)
        {
            return CreateDefaultSessionFactory(DefaultProductId, DefaultServiceId, keyManagementService, metastore);
        }

        private static SessionFactory CreateDefaultSessionFactory(
            string productId,
            string serviceId,
            IKeyManagementService keyManagementService,
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

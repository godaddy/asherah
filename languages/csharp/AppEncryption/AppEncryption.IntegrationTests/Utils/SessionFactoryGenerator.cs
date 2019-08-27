using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        public static SessionFactory CreateDefaultSessionFactory(
            KeyManagementService keyManagementService, IMetastorePersistence<JObject> metastorePersistence)
        {
            return CreateDefaultSessionFactory(DefaultProductId, DefaultSystemId, keyManagementService, metastorePersistence);
        }

        private static SessionFactory CreateDefaultSessionFactory(
            string productId,
            string systemId,
            KeyManagementService keyManagementService,
            IMetastorePersistence<JObject> metastorePersistence)
        {
            return SessionFactory.NewBuilder(productId, systemId)
                .WithMetaStorePersistence(metastorePersistence)
                .WithNeverExpiredCryptoPolicy()
                .WithKeyManagementService(keyManagementService)
                .Build();
        }
    }
}

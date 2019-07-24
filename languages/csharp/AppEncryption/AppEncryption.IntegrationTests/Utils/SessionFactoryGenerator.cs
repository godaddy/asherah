using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        public static AppEncryptionSessionFactory CreateDefaultAppEncryptionSessionFactory(
            KeyManagementService keyManagementService, IMetastorePersistence<JObject> metastorePersistence)
        {
            return CreateDefaultAppEncryptionSessionFactory(DefaultProductId, DefaultSystemId, keyManagementService, metastorePersistence);
        }

        private static AppEncryptionSessionFactory CreateDefaultAppEncryptionSessionFactory(
            string productId,
            string systemId,
            KeyManagementService keyManagementService,
            IMetastorePersistence<JObject> metastorePersistence)
        {
            return AppEncryptionSessionFactory.NewBuilder(productId, systemId)
                .WithMetaStorePersistence(metastorePersistence)
                .WithNeverExpiredCryptoPolicy()
                .WithKeyManagementService(keyManagementService)
                .Build();
        }
    }
}

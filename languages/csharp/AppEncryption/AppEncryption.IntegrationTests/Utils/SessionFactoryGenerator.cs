using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class SessionFactoryGenerator
    {
        public static AppEncryptionSessionFactory CreateDefaultAppEncryptionSessionFactory()
        {
            return CreateDefaultAppEncryptionSessionFactory(DefaultProductId, DefaultSystemId);
        }

        public static AppEncryptionSessionFactory CreateDefaultAppEncryptionSessionFactory(string productId, string systemId)
        {
            return AppEncryptionSessionFactory.NewBuilder(productId, systemId)
                .WithMemoryPersistence()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(KeyManagementStaticMasterKey)
                .Build();
        }
    }
}

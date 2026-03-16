using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Logging;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    /// <summary>
    /// Creates <see cref="GoDaddy.Asherah.AppEncryption.Core.SessionFactory"/> instances for integration tests.
    /// </summary>
    public static class CoreSessionFactoryGenerator
    {
        /// <summary>
        /// Creates a Core SessionFactory with the given key management service and an in-memory key metastore.
        /// </summary>
        public static GoDaddy.Asherah.AppEncryption.Core.SessionFactory CreateDefaultSessionFactory(
            IKeyManagementService keyManagementService)
        {
            return CreateDefaultSessionFactory(keyManagementService, new InMemoryKeyMetastore());
        }

        /// <summary>
        /// Creates a Core SessionFactory with the given key management service and key metastore.
        /// </summary>
        public static GoDaddy.Asherah.AppEncryption.Core.SessionFactory CreateDefaultSessionFactory(
            IKeyManagementService keyManagementService,
            IKeyMetastore keyMetastore)
        {
            return CreateDefaultSessionFactory(
                DefaultProductId,
                DefaultServiceId,
                keyManagementService,
                keyMetastore,
                TestLoggerFactory.LoggerFactory.CreateLogger("CoreSessionFactoryGenerator"));
        }

        private static GoDaddy.Asherah.AppEncryption.Core.SessionFactory CreateDefaultSessionFactory(
            string productId,
            string serviceId,
            IKeyManagementService keyManagementService,
            IKeyMetastore keyMetastore,
            ILogger logger)
        {
            var cryptoPolicy = new NeverExpiredCryptoPolicy();
            return GoDaddy.Asherah.AppEncryption.Core.SessionFactory
                .NewBuilder(productId, serviceId)
                .WithKeyMetastore(keyMetastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .WithLogger(logger)
                .Build();
        }
    }
}

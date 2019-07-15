using System;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace GoDaddy.AppServices.AppEncryption.IntegrationTests
{
    public class Configuration
    {
        static Configuration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();
            MetaStoreType = config["metaStoreType"];
            KmsType = config["kmsType"];
            KeyManagementService = CreateKeyManagementService(KmsType);
            MetastorePersistence = CreateMetaStorePersistence(MetaStoreType);
        }

        public static string MetaStoreType { get; }

        public static string KmsType { get; }

        public static KeyManagementService KeyManagementService { get; }

        public static IMetastorePersistence<JObject> MetastorePersistence { get; }

        private static IMetastorePersistence<JObject> CreateMetaStorePersistence(string metaStoreType)
        {
            if (metaStoreType.Equals("memory", StringComparison.InvariantCultureIgnoreCase))
            {
                return new MemoryPersistenceImpl<JObject>();
            }

            return null;
        }

        private static KeyManagementService CreateKeyManagementService(string kmsType)
        {
            if (kmsType.Equals("static", StringComparison.InvariantCultureIgnoreCase))
            {
                return new StaticKeyManagementServiceImpl("secretmasterkey!");
            }

            return null;
        }
    }
}

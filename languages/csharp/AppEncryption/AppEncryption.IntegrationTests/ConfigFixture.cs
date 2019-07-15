using System;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace GoDaddy.AppServices.AppEncryption.IntegrationTests
{
    public class ConfigFixture
    {
        public ConfigFixture()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();
            MetaStoreType = config["metaStoreType"];
            KmsType = config["kmsType"];
            KeyManagementService = CreateKeyManagementService(KmsType);
            MetastorePersistence = CreateMetaStorePersistence(MetaStoreType);
        }

        public string MetaStoreType { get; }

        public string KmsType { get; }

        public KeyManagementService KeyManagementService { get; }

        public IMetastorePersistence<JObject> MetastorePersistence { get; }

        private IMetastorePersistence<JObject> CreateMetaStorePersistence(string metaStoreType)
        {
            if (metaStoreType.Equals("memory", StringComparison.InvariantCultureIgnoreCase))
            {
                return new MemoryPersistenceImpl<JObject>();
            }

            return null;
        }

        private KeyManagementService CreateKeyManagementService(string kmsType)
        {
            if (kmsType.Equals("static", StringComparison.InvariantCultureIgnoreCase))
            {
                return new StaticKeyManagementServiceImpl("secretmasterkey!");
            }

            return null;
        }
    }
}

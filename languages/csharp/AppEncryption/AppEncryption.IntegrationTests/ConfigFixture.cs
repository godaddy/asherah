using System;
using System.Collections.Generic;
using System.Linq;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests
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
            if (metaStoreType.Equals(MetastoreADO, StringComparison.InvariantCultureIgnoreCase))
            {
                return AdoMetastorePersistenceImpl
                    .NewBuilder(MySqlClientFactory.Instance, Environment.GetEnvironmentVariable("METASTORE_ADO_CONNECTIONSTRING"))
                    .Build();
            }

            if (metaStoreType.Equals(MetastoreDynamoDb, StringComparison.InvariantCultureIgnoreCase))
            {
                return DynamoDbMetastorePersistenceImpl.NewBuilder().Build();
            }

            return new MemoryPersistenceImpl<JObject>();
        }

        private KeyManagementService CreateKeyManagementService(string kmsType)
        {
            if (kmsType.Equals(KeyManagementAWS, StringComparison.InvariantCultureIgnoreCase))
            {
                string regionToArnString = Environment.GetEnvironmentVariable("KMS_AWS_REGION_DICTIONARY");
                Dictionary<string, string> regionToArnDictionary = regionToArnString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split('='))
                    .ToDictionary(split => split[0], split => split[1]);
                return AwsKeyManagementServiceImpl.NewBuilder(
                    regionToArnDictionary, Environment.GetEnvironmentVariable("KMS_AWS_PREFERRED_REGION"))
                    .Build();
            }

            return new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Exceptions;
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
            // Load the config file name from environment variables. If not found, default to config.yaml
            IConfigurationRoot config;
            try
            {
                config = new ConfigurationBuilder()
                    .AddYamlFile(Environment.GetEnvironmentVariable(ConfigFile))
                    .Build();
            }
            catch (ArgumentException)
            {
                config = new ConfigurationBuilder()
                    .AddYamlFile(DefaultConfigFile)
                    .Build();
            }

            MetaStoreType = config[Constants.MetaStoreType];
            KmsType = config[Constants.KmsType];
            KeyManagementService = CreateKeyManagementService(KmsType);
            MetastorePersistence = CreateMetaStorePersistence(MetaStoreType);
        }

        public KeyManagementService KeyManagementService { get; }

        public IMetastorePersistence<JObject> MetastorePersistence { get; }

        private string MetaStoreType { get; }

        private string KmsType { get; }

        private static IMetastorePersistence<JObject> CreateMetaStorePersistence(string metaStoreType)
        {
            if (metaStoreType.Equals(MetastoreAdo, StringComparison.InvariantCultureIgnoreCase))
            {
                string adoConnectionString = Environment.GetEnvironmentVariable(AdoConnectionString);

                if (string.IsNullOrWhiteSpace(adoConnectionString))
                {
                    throw new AppEncryptionException("Missing ADO connection string");
                }

                return AdoMetastorePersistenceImpl
                    .NewBuilder(MySqlClientFactory.Instance, adoConnectionString)
                    .Build();
            }

            if (metaStoreType.Equals(MetastoreDynamoDb, StringComparison.InvariantCultureIgnoreCase))
            {
                return DynamoDbMetastorePersistenceImpl.NewBuilder().Build();
            }

            return new MemoryPersistenceImpl<JObject>();
        }

        private static KeyManagementService CreateKeyManagementService(string kmsType)
        {
            if (kmsType.Equals(KeyManagementAws, StringComparison.InvariantCultureIgnoreCase))
            {
                string regionToArnTuples = Environment.GetEnvironmentVariable(KmsAwsRegionTuples);

                if (string.IsNullOrWhiteSpace(regionToArnTuples))
                {
                    throw new AppEncryptionException("Missing AWS Region ARN tuples");
                }

                Dictionary<string, string> regionToArnDictionary =
                    regionToArnTuples.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Split('='))
                        .ToDictionary(split => split[0], split => split[1]);

                return AwsKeyManagementServiceImpl.NewBuilder(
                        regionToArnDictionary, Environment.GetEnvironmentVariable(KmsAwsPreferredRegion))
                    .Build();
            }

            return new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);
        }
    }
}

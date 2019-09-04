using System;
using System.Collections.Generic;
using System.Linq;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.Kms;
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
        private readonly IConfigurationRoot config;

        public ConfigFixture()
        {
            // Load the config file name from environment variables. If not found, default to config.yaml
            string configFile = Environment.GetEnvironmentVariable(ConfigFile);
            if (string.IsNullOrWhiteSpace(configFile))
            {
                configFile = DefaultConfigFile;
            }

            config = new ConfigurationBuilder()
                .AddYamlFile(configFile)
                .Build();

            MetastoreType = GetParam(Constants.MetastoreType);
            if (string.IsNullOrWhiteSpace(MetastoreType))
            {
                MetastoreType = DefaultMetastoreType;
            }

            KmsType = GetParam(Constants.KmsType);
            if (string.IsNullOrWhiteSpace(KmsType))
            {
                KmsType = DefaultKeyManagementType;
            }

            KeyManagementService = CreateKeyManagementService();
            Metastore = CreateMetastore();
        }

        public KeyManagementService KeyManagementService { get; }

        public IMetastore<JObject> Metastore { get; }

        private string PreferredRegion { get; set; }

        private string MetastoreType { get; }

        private string KmsType { get; }

        private static string GetEnvVariable(string input)
        {
            return string.Concat(input.Select(x => char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToUpper();
        }

        private string GetParam(string paramName)
        {
            string paramValue = Environment.GetEnvironmentVariable(GetEnvVariable(paramName));
            if (string.IsNullOrWhiteSpace(paramValue))
            {
                paramValue = config[paramName];
            }

            return paramValue;
        }

        private IMetastore<JObject> CreateMetastore()
        {
            if (MetastoreType.Equals(MetastoreAdo, StringComparison.InvariantCultureIgnoreCase))
            {
                string metastoreAdoConnectionString = GetParam(MetastoreAdoConnectionString);

                if (string.IsNullOrWhiteSpace(metastoreAdoConnectionString))
                {
                    throw new AppEncryptionException("Missing ADO connection string");
                }

                return AdoMetastoreImpl
                    .NewBuilder(MySqlClientFactory.Instance, metastoreAdoConnectionString)
                    .Build();
            }

            if (MetastoreType.Equals(MetastoreDynamoDb, StringComparison.InvariantCultureIgnoreCase))
            {
                return DynamoDbMetastoreImpl.NewBuilder().Build();
            }

            return new InMemoryMetastoreImpl<JObject>();
        }

        private KeyManagementService CreateKeyManagementService()
        {
            if (KmsType.Equals(KeyManagementAws, StringComparison.InvariantCultureIgnoreCase))
            {
                string regionToArnTuples = GetParam(KmsAwsRegionTuples);

                if (string.IsNullOrWhiteSpace(regionToArnTuples))
                {
                    throw new AppEncryptionException("Missing AWS Region ARN tuples");
                }

                Dictionary<string, string> regionToArnDictionary =
                    regionToArnTuples.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Split('='))
                        .ToDictionary(split => split[0], split => split[1]);

                PreferredRegion = GetParam(KmsAwsPreferredRegion);
                if (string.IsNullOrWhiteSpace(PreferredRegion))
                {
                    PreferredRegion = DefaultPreferredRegion;
                }

                return AwsKeyManagementServiceImpl.NewBuilder(regionToArnDictionary, PreferredRegion)
                    .Build();
            }

            return new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);
        }
    }
}

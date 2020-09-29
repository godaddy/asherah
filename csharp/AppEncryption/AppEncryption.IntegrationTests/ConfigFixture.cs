using System;
using System.Linq;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests
{
    public class ConfigFixture : IDisposable
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

            KeyManagementService = CreateKeyManagementService();
            Metastore = CreateMetastore();
        }

        public IConfiguration Configuration => config;

        public KeyManagementService KeyManagementService { get; }

        public IMetastore<JObject> Metastore { get; }

        public void Dispose()
        {
            KeyManagementService.Dispose();
        }

        private static string GetEnvVariable(string input)
        {
            return string.Concat(input.Select(x => char.IsUpper(x) ? "_" + x : x.ToString())).ToUpper();
        }

        private IMetastore<JObject> CreateMetastore()
        {
            string envMetaStoreType = Environment.GetEnvironmentVariable(GetEnvVariable(Constants.MetastoreType));
            if (!string.IsNullOrWhiteSpace(envMetaStoreType))
            {
                config[MetastoreSelector<JObject>.MetastoreType] = envMetaStoreType;
            }

            if (config[MetastoreSelector<JObject>.MetastoreType].Equals(MetastoreAdo, StringComparison.InvariantCultureIgnoreCase))
            {
                string envAdoConnStr = Environment.GetEnvironmentVariable(GetEnvVariable(MetastoreAdoConnectionString));
                if (!string.IsNullOrWhiteSpace(envAdoConnStr))
                {
                    config[MetastoreSelector<JObject>.MetastoreAdoConnectionString] = envAdoConnStr;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return null;
                }
            }

            return MetastoreSelector<JObject>.SelectMetastoreWithConfiguration(config);
        }

        private KeyManagementService CreateKeyManagementService()
        {
            string envKmsType = Environment.GetEnvironmentVariable(GetEnvVariable(Constants.KmsType));
            if (!string.IsNullOrWhiteSpace(envKmsType))
            {
                config[KeyManagementServiceSelector.KmsType] = envKmsType;
            }

            if (string.IsNullOrWhiteSpace(config[KeyManagementServiceSelector.KmsType]))
            {
                config[KeyManagementServiceSelector.KmsType] = DefaultKeyManagementType;
            }

            if (string.IsNullOrWhiteSpace(config[KeyManagementServiceSelector.KmsStaticKey]))
            {
                config[KeyManagementServiceSelector.KmsStaticKey] = KeyManagementStaticMasterKey;
            }

            var cryptoPolicy = BasicExpiringCryptoPolicy.BuildWithConfiguration(config);
            return KeyManagementServiceSelector.SelectKmsWithConfiguration(cryptoPolicy, config);
        }
    }
}

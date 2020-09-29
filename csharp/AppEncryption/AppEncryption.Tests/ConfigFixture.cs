using System;
using Microsoft.Extensions.Configuration;

using static GoDaddy.Asherah.AppEncryption.Tests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.Tests
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
        }

        public IConfiguration Configuration => config;
    }
}

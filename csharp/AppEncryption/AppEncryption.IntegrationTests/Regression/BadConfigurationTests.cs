using System;
using System.Collections.Generic;
using System.Text;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class BadConfigurationTests
    {
        [Theory]
        [ClassData(typeof(TestBadConfigurations))]
        private void TestBadConfigurations(IConfiguration configuration, Type exceptionType)
        {
            StringBuilder sb = new StringBuilder();
            if (configuration != null)
            {
                foreach (var configurationEntries in configuration.GetChildren())
                {
                    sb.AppendLine($"{configurationEntries.Key}={configurationEntries.Value}");
                }
            }

            Assert.Throws(
                exceptionType,
                () =>
                {
                    RunPartitionTest(configuration, NumIterations, DefaultPartitionId, PayloadSizeBytes);
                    throw new Exception(sb.ToString());
                });
        }

        private void RunPartitionTest(IConfiguration configuration, int testIterations, string partitionId, int payloadSizeBytesBase)
        {
            using (SessionFactory sessionFactory =
                SessionFactoryGenerator.CreateDefaultSessionFactory(
                    configuration))
            {
                using (Session<JObject, byte[]> session = sessionFactory.GetSessionJson(partitionId))
                {
                    Dictionary<string, byte[]> dataStore = new Dictionary<string, byte[]>();

                    string partitionPart = $"partition-{partitionId}-";

                    for (int i = 0; i < testIterations; i++)
                    {
                        // Note the size will be slightly larger since we're adding extra unique meta
                        JObject jObject = PayloadGenerator.CreateRandomJsonPayload(payloadSizeBytesBase);
                        string keyPart = $"iteration-{i}";
                        jObject["payload"] = partitionPart + keyPart;

                        dataStore.Add(keyPart, session.Encrypt(jObject));
                    }

                    foreach (KeyValuePair<string, byte[]> keyValuePair in dataStore)
                    {
                        JObject decryptedObject = session.Decrypt(keyValuePair.Value);
                    }
                }
            }
        }
    }
}

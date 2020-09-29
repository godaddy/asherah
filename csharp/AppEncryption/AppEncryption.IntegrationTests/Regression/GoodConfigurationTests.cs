using System.Collections.Generic;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class GoodConfigurationTests
    {
        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        public void TestAllGoodConfigurations(IConfiguration configuration)
        {
            RunPartitionTest(configuration, NumIterations, DefaultPartitionId, PayloadSizeBytes);
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

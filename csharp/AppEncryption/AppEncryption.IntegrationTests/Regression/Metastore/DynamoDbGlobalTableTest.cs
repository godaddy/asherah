using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore;
using GoDaddy.Asherah.AppEncryption.Tests.Fixtures;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression.Metastore
{
    [Collection("Configuration collection")]
    public class DynamoDbGlobalTableTest : IClassFixture<DynamoDbContainerFixture>, IDisposable
    {
        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string DefaultTableName = "EncryptionKey";
        private const string DefaultRegion = "us-west-2";

        private readonly ConfigFixture _configFixture;
        private readonly string _serviceUrl;
        private readonly AmazonDynamoDBClient _tempDynamoDbClient;

        public DynamoDbGlobalTableTest(DynamoDbContainerFixture dynamoDbContainerFixture, ConfigFixture configFixture)
        {
            _serviceUrl = dynamoDbContainerFixture.GetServiceUrl();
            _configFixture = configFixture;

            var amazonDynamoDbConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = _serviceUrl,
                AuthenticationRegion = DefaultRegion,
            };
            _tempDynamoDbClient = new AmazonDynamoDBClient(amazonDynamoDbConfig);
            var request = new CreateTableRequest
            {
                TableName = DefaultTableName,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition(PartitionKey, ScalarAttributeType.S),
                    new AttributeDefinition(SortKey, ScalarAttributeType.N),
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement(PartitionKey, KeyType.HASH),
                    new KeySchemaElement(SortKey, KeyType.RANGE),
                },
                ProvisionedThroughput = new ProvisionedThroughput(1L, 1L),
            };
            _tempDynamoDbClient.CreateTableAsync(request).Wait();
        }

        public void Dispose()
        {
            try
            {
                _tempDynamoDbClient?.DeleteTableAsync(DefaultTableName).Wait();
            }
            catch (AggregateException)
            {
                // Table may not exist.
            }
        }

        private GoDaddy.Asherah.AppEncryption.Core.SessionFactory GetSessionFactory(bool withKeySuffix, string region)
        {
            var options = new DynamoDbMetastoreOptions
            {
                KeyRecordTableName = DefaultTableName,
                KeySuffix = withKeySuffix ? region : string.Empty,
            };

            // Reuse the same client so both "regions" hit the same table (global table simulation).
            var metastore = DynamoDbMetastore.NewBuilder()
                .WithDynamoDbClient(_tempDynamoDbClient)
                .WithOptions(options)
                .Build();

            return CoreSessionFactoryGenerator.CreateDefaultSessionFactory(
                _configFixture.KeyManagementService,
                metastore);
        }

        [Fact]
        private void TestRegionSuffix()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;
            byte[] decryptedBytes;

            using (var sessionFactory = GetSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            using (var sessionFactory = GetSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);
        }

        [Fact]
        private void TestRegionSuffixBackwardCompatibility()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;
            byte[] decryptedBytes;

            using (var sessionFactory = GetSessionFactory(false, DefaultRegion))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            using (var sessionFactory = GetSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);
        }

        [Fact]
        private void TestCrossRegionDecryption()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;
            byte[] decryptedBytes;

            using (var sessionFactory = GetSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            using (var sessionFactory = GetSessionFactory(true, "us-east-1"))
            {
                using (IEncryptionSession session = sessionFactory.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);
        }
    }
}

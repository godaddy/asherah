using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Tests;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    [Collection("Configuration collection")]
    public class DynamoDbGlobalTableTest : IClassFixture<DynamoDBContainerFixture>, IClassFixture<MetricsFixture>
    {
        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string DefaultTableName = "EncryptionKey";

        private readonly ConfigFixture configFixture;
        private readonly DynamoDbMetastoreImpl dynamoDbMetastoreImpl;
        private readonly DynamoDbMetastoreImpl dynamoDbMetastoreImplWithKeySuffix;

        public DynamoDbGlobalTableTest(DynamoDBContainerFixture dynamoDbContainerFixture, ConfigFixture configFixture)
        {
            this.configFixture = configFixture;

            // Use AWS SDK to create client and initialize table
            AmazonDynamoDBConfig amazonDynamoDbConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = dynamoDbContainerFixture.ServiceUrl,
                AuthenticationRegion = "us-west-2",
            };
            IAmazonDynamoDB tempDynamoDbClient = new AmazonDynamoDBClient(amazonDynamoDbConfig);
            CreateTableRequest request = new CreateTableRequest
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
            tempDynamoDbClient.CreateTableAsync(request).Wait();

            // Use a builder without the suffix
            dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder("us-west-2")
                .WithEndPointConfiguration(dynamoDbContainerFixture.ServiceUrl, "us-west-2")
                .Build();

            // Connect to the same metastore but initialize it with a key suffix
            dynamoDbMetastoreImplWithKeySuffix = DynamoDbMetastoreImpl.NewBuilder("us-west-2")
                .WithEndPointConfiguration(dynamoDbContainerFixture.ServiceUrl, "us-west-2")
                .WithKeySuffix()
                .Build();
        }

        [Fact]
        private void TestRegionSuffixBackwardCompatibility()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] decryptedBytes;
            byte[] dataRowRecordBytes;

            // Encrypt originalPayloadString with metastore without key suffix
            using (SessionFactory sessionFactory = SessionFactoryGenerator
                .CreateDefaultSessionFactory(configFixture.KeyManagementService, dynamoDbMetastoreImpl))
            {
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
                {
                    dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
                }
            }

            // Decrypt dataRowString with metastore with key suffix
            using (SessionFactory sessionFactory = SessionFactoryGenerator
                .CreateDefaultSessionFactory(configFixture.KeyManagementService, dynamoDbMetastoreImplWithKeySuffix))
            {
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
                {
                    // Decrypt the payload
                    decryptedBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            // Verify that we were able to decrypt with a suffixed builder
            Assert.Equal(decryptedBytes, originalPayload);
        }
    }
}

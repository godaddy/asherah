using System;
using System.Collections.Generic;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Tests;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class DynamoDbCompatibilityTest : IClassFixture<DynamoDBContainerFixture>, IClassFixture<MetricsFixture>
    {
        private const string StaticMasterKey = "thisIsAStaticMasterKeyForTesting";
        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string DefaultTableName = "EncryptionKey";

        private readonly DynamoDbMetastoreImpl dynamoDbMetastoreImpl;
        private readonly DynamoDbMetastoreImpl dynamoDbMetastoreImplWithKeySuffix;

        public DynamoDbCompatibilityTest(DynamoDBContainerFixture dynamoDbContainerFixture)
        {
            dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder()
                .WithEndPointConfiguration(dynamoDbContainerFixture.ServiceUrl, "us-west-2")
                .Build();

            // Create table schema
            IAmazonDynamoDB amazonDynamoDbClient = dynamoDbMetastoreImpl.GetClient();
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
            amazonDynamoDbClient.CreateTableAsync(request).Wait();

            // Connect to the same metastore but initialize it with a key suffix
            dynamoDbMetastoreImplWithKeySuffix = DynamoDbMetastoreImpl.NewBuilder()
                .WithEndPointConfiguration(dynamoDbContainerFixture.ServiceUrl, "us-west-2")
                .WithKeySuffix("us-west-2")
                .Build();
        }

        [Fact]
        private void TestRegionSuffixBackwardCompatibility()
        {
            string dataRowString;
            string originalPayloadString;
            string decryptedPayloadString;

            // Encrypt originalPayloadString with metastore without key suffix
            using (SessionFactory sessionFactory = SessionFactory.NewBuilder("productId", "reference_app")
                .WithMetastore(dynamoDbMetastoreImpl)
                .WithCryptoPolicy(new NeverExpiredCryptoPolicy())
                .WithKeyManagementService(new StaticKeyManagementServiceImpl(StaticMasterKey))
                .Build())
            {
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
                {
                    originalPayloadString = "mysupersecretpayload";
                    byte[] dataRowRecordBytes =
                        sessionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

                    // Consider this us "persisting" the DRR
                    dataRowString = Convert.ToBase64String(dataRowRecordBytes);
                }
            }

            // Decrypt dataRowString with metastore with key suffix
            using (SessionFactory sessionFactory = SessionFactory.NewBuilder("productId", "reference_app")
                .WithMetastore(dynamoDbMetastoreImplWithKeySuffix)
                .WithCryptoPolicy(new NeverExpiredCryptoPolicy())
                .WithKeyManagementService(new StaticKeyManagementServiceImpl(StaticMasterKey))
                .Build())
            {
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
                {
                    byte[] newDataRowRecordBytes = Convert.FromBase64String(dataRowString);

                    // Decrypt the payload
                    decryptedPayloadString = Encoding.UTF8.GetString(sessionBytes.Decrypt(newDataRowRecordBytes));
                }
            }

            // Verify that we were able to decrypt with a suffixed builder
            Assert.Equal(decryptedPayloadString, originalPayloadString);
        }
    }
}

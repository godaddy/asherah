using System;
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
  public class DynamoDbGlobalTableTest : IClassFixture<DynamoDBContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
  {
    private const string PartitionKey = "Id";
    private const string SortKey = "Created";
    private const string DefaultTableName = "EncryptionKey";
    private const string DefaultRegion = "us-west-2";

    private readonly ConfigFixture configFixture;
    private readonly string serviceUrl;

    private AmazonDynamoDBClient tempDynamoDbClient;

    public DynamoDbGlobalTableTest(DynamoDBContainerFixture dynamoDbContainerFixture, ConfigFixture configFixture)
    {
      serviceUrl = dynamoDbContainerFixture.GetServiceUrl();
      this.configFixture = configFixture;

      // Use AWS SDK to create client and initialize table
      AmazonDynamoDBConfig amazonDynamoDbConfig = new AmazonDynamoDBConfig
      {
        ServiceURL = serviceUrl,
        AuthenticationRegion = "us-west-2",
      };
      tempDynamoDbClient = new AmazonDynamoDBClient(amazonDynamoDbConfig);
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
    }

    public void Dispose()
    {
      try
      {
        DeleteTableResponse deleteTableResponse = tempDynamoDbClient
            .DeleteTableAsync(DefaultTableName)
            .Result;
      }
      catch (AggregateException)
      {
        // There is no such table.
      }
      GC.SuppressFinalize(this);
    }

    private SessionFactory GetSessionFactory(bool withKeySuffix, string region)
    {
      DynamoDbMetastoreImpl.IBuildStep builder = DynamoDbMetastoreImpl.NewBuilder(region)
          .WithEndPointConfiguration(serviceUrl, DefaultRegion);

      if (withKeySuffix)
      {
        builder = builder.WithKeySuffix();
      }

      DynamoDbMetastoreImpl dynamoDbMetastore = builder.Build();
      return SessionFactoryGenerator.CreateDefaultSessionFactory(configFixture.KeyManagementService, dynamoDbMetastore);
    }

    [Fact]
    private void TestRegionSuffix()
    {
      byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
      byte[] decryptedBytes;
      byte[] dataRowRecordBytes;

      // Encrypt originalPayloadString with metastore with key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(true, DefaultRegion))
      {
        using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
        {
          dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
        }
      }

      // Decrypt dataRowString with metastore with key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(true, DefaultRegion))
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

    [Fact]
    private void TestRegionSuffixBackwardCompatibility()
    {
      byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
      byte[] decryptedBytes;
      byte[] dataRowRecordBytes;

      // Encrypt originalPayloadString with metastore without key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(false, DefaultRegion))
      {
        using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
        {
          dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
        }
      }

      // Decrypt dataRowString with metastore with key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(true, DefaultRegion))
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

    [Fact]
    private void TestCrossRegionDecryption()
    {
      byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
      byte[] decryptedBytes;
      byte[] dataRowRecordBytes;

      // Encrypt originalPayloadString with metastore without key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(true, DefaultRegion))
      {
        using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("shopper123"))
        {
          dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
        }
      }

      // Decrypt dataRowString with metastore with key suffix
      using (SessionFactory sessionFactory = GetSessionFactory(true, "us-east-1"))
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

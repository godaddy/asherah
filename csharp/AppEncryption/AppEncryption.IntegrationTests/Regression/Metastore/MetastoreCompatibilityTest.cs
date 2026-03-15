using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Kms;
using GoDaddy.Asherah.AppEncryption.Tests.Fixtures;
using Xunit;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression.Metastore
{
    [Collection("Configuration collection")]
    public class MetastoreCompatibilityTest : IClassFixture<DynamoDbContainerFixture>, IDisposable
    {
        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string DefaultTableName = "EncryptionKey";
        private const string DefaultRegion = "us-west-2";
        private const string OtherRegion = "us-east-1";

        private readonly AmazonDynamoDBClient _dynamoDbClient;
        private readonly string _serviceUrl;
        private readonly StaticKeyManagementService _keyManagementService;

        public MetastoreCompatibilityTest(DynamoDbContainerFixture dynamoDbContainerFixture)
        {
            _serviceUrl = dynamoDbContainerFixture.GetServiceUrl();

            _dynamoDbClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
            {
                ServiceURL = _serviceUrl,
                AuthenticationRegion = DefaultRegion,
            });

            var createTableRequest = new CreateTableRequest
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
            _dynamoDbClient.CreateTableAsync(createTableRequest).Wait();

            _keyManagementService = new StaticKeyManagementService(KeyManagementStaticMasterKey);
        }

        private SessionFactory GetLegacySessionFactory(bool withKeySuffix, string region)
        {
            DynamoDbMetastoreImpl.IBuildStep builder = DynamoDbMetastoreImpl.NewBuilder(region)
                .WithEndPointConfiguration(_serviceUrl, DefaultRegion)
                .WithTableName(DefaultTableName);

            if (withKeySuffix)
            {
                builder = builder.WithKeySuffix();
            }

            DynamoDbMetastoreImpl metastore = builder.Build();
            return SessionFactoryGenerator.CreateDefaultSessionFactory(_keyManagementService, metastore);
        }

        private GoDaddy.Asherah.AppEncryption.Core.SessionFactory GetCoreSessionFactory(bool withKeySuffix, string region)
        {
            var options = new DynamoDbMetastoreOptions
            {
                KeyRecordTableName = DefaultTableName,
                KeySuffix = withKeySuffix ? region : string.Empty,
            };
            var metastore = DynamoDbMetastore.NewBuilder()
                .WithDynamoDbClient(_dynamoDbClient)
                .WithOptions(options)
                .Build();
            return CoreSessionFactoryGenerator.CreateDefaultSessionFactory(_keyManagementService, metastore);
        }

        [Fact]
        private void TestRegionSuffixLegacyToCore()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (SessionFactory legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (SessionFactory legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedAgainBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        [Fact]
        private void TestRegionSuffixCoreToLegacy()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedAgainBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        [Fact]
        private void TestRegionSuffixBackwardCompatibilityLegacyToCore()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (SessionFactory legacy = GetLegacySessionFactory(false, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (SessionFactory legacy = GetLegacySessionFactory(false, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedAgainBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        [Fact]
        private void TestRegionSuffixBackwardCompatibilityCoreToLegacy()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (var core = GetCoreSessionFactory(false, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (var core = GetCoreSessionFactory(false, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedAgainBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        [Fact]
        private void TestCrossRegionDecryptionLegacyToCore()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (SessionFactory legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    dataRowRecordBytes = sessionBytes.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var core = GetCoreSessionFactory(true, OtherRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (SessionFactory legacy = GetLegacySessionFactory(true, DefaultRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedAgainBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        [Fact]
        private void TestCrossRegionDecryptionCoreToLegacy()
        {
            byte[] originalPayload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    dataRowRecordBytes = session.Encrypt(originalPayload);
                }
            }

            byte[] decryptedBytes;
            using (var legacy = GetLegacySessionFactory(true, OtherRegion))
            {
                using (var sessionBytes = legacy.GetSessionBytes("shopper123"))
                {
                    decryptedBytes = sessionBytes.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedBytes);

            byte[] decryptedAgainBytes;
            using (var core = GetCoreSessionFactory(true, DefaultRegion))
            {
                using (IEncryptionSession session = core.GetSession("shopper123"))
                {
                    decryptedAgainBytes = session.Decrypt(dataRowRecordBytes);
                }
            }

            Assert.Equal(originalPayload, decryptedAgainBytes);
        }

        public void Dispose()
        {
            _keyManagementService?.Dispose();
            try
            {
                _dynamoDbClient?.DeleteTableAsync(DefaultTableName).Wait();
            }
            catch (AggregateException)
            {
                // Table may not exist.
            }

            _dynamoDbClient?.Dispose();
        }
    }
}

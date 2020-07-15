using System;
using System.Collections.Generic;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Exceptions;
using LanguageExt;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.Persistence.DynamoDbMetastoreImpl;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class DynamoDbMetastoreImplTest : IClassFixture<DynamoDBContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
    {
        private const string TestKey = "some_key";
        private const string DynamoDbPort = "8000";
        private const string Region = "us-west-2";
        private const string TestKeyWithRegionSuffix = TestKey + "_" + Region;

        private readonly IAmazonDynamoDB amazonDynamoDbClient;

        private readonly Dictionary<string, object> keyRecord = new Dictionary<string, object>
        {
            {
                "ParentKeyMeta", new Dictionary<string, object>
                {
                    { "KeyId", "_SK_api_ecomm" },
                    { "Created", 1541461380 },
                }
            },
            { "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr" },
            { "Created", 1541461380 },
        };

        private readonly Table table;
        private readonly DynamoDbMetastoreImpl dynamoDbMetastoreImpl;
        private readonly DateTimeOffset created = DateTimeOffset.Now.AddDays(-1);

        public DynamoDbMetastoreImplTest(DynamoDBContainerFixture dynamoDbContainerFixture)
        {
            dynamoDbMetastoreImpl = NewBuilder(Region)
                .WithEndPointConfiguration(dynamoDbContainerFixture.ServiceUrl, "us-west-2")
                .Build();
            amazonDynamoDbClient = dynamoDbMetastoreImpl.DbClient;

            CreateTableSchema(amazonDynamoDbClient, dynamoDbMetastoreImpl.TableName);

            table = Table.LoadTable(amazonDynamoDbClient, dynamoDbMetastoreImpl.TableName);

            JObject jObject = JObject.FromObject(keyRecord);
            Document document = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = created.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(jObject.ToString()),
            };

            table.PutItemAsync(document).Wait();

            document = new Document
            {
                [PartitionKey] = TestKeyWithRegionSuffix,
                [SortKey] = created.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(jObject.ToString()),
            };

            table.PutItemAsync(document).Wait();
        }

        public void Dispose()
        {
            try
            {
                DeleteTableResponse deleteTableResponse = amazonDynamoDbClient
                    .DeleteTableAsync(dynamoDbMetastoreImpl.TableName)
                    .Result;
            }
            catch (AggregateException)
            {
                // There is no such table.
            }
        }

        private void CreateTableSchema(IAmazonDynamoDB client, string tableName)
        {
            CreateTableRequest request = new CreateTableRequest
            {
                TableName = tableName,
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

            CreateTableResponse createTableResponse = client.CreateTableAsync(request).Result;
        }

        [Fact]
        private void TestLoadSuccess()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.Load(TestKey, created);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        private void TestLoadWithNoResultShouldReturnEmpty()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.Load("fake_key", created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadWithFailureShouldReturnEmpty()
        {
            Dispose();
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.Load(TestKey, created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadLatestWithSingleRecord()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.LoadLatest(TestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        private void TestLoadLatestWithSingleRecordAndSuffix()
        {
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithEndPointConfiguration("http://localhost:" + DynamoDbPort, Region)
                .WithKeySuffix()
                .Build();

            Option<JObject> actualJsonObject = dbMetastoreImpl.LoadLatest(TestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        private void TestLoadLatestWithMultipleRecords()
        {
            DateTimeOffset createdMinusOneHour = created.AddHours(-1);
            DateTimeOffset createdPlusOneHour = created.AddHours(1);
            DateTimeOffset createdMinusOneDay = created.AddDays(-1);
            DateTimeOffset createdPlusOneDay = created.AddDays(1);

            // intentionally mixing up insertion order
            Document documentPlusOneHour = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdPlusOneHour.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdPlusOneHour },
                }.ToString()),
            };
            table.PutItemAsync(documentPlusOneHour).Wait();

            Document documentPlusOneDay = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdPlusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdPlusOneDay },
                }.ToString()),
            };
            table.PutItemAsync(documentPlusOneDay).Wait();

            Document documentMinusOneHour = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdMinusOneHour.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneHour },
                }.ToString()),
            };
            table.PutItemAsync(documentMinusOneHour).Wait();

            Document documentMinusOneDay = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdMinusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneDay },
                }.ToString()),
            };
            table.PutItemAsync(documentMinusOneDay).Wait();

            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.LoadLatest(TestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(createdPlusOneDay, ((JObject)actualJsonObject).GetValue("mytime")));
        }

        [Fact]
        private void TestLoadLatestWithNoResultShouldReturnEmpty()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.LoadLatest("fake_key");

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadLatestWithFailureShouldReturnEmpty()
        {
            Dispose();
            Option<JObject> actualJsonObject = dynamoDbMetastoreImpl.LoadLatest(TestKey);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestStore()
        {
            bool actualValue = dynamoDbMetastoreImpl.Store(TestKey, DateTimeOffset.Now, JObject.FromObject(keyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        private void TestStoreWithSuffixSuccess()
        {
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithEndPointConfiguration("http://localhost:" + DynamoDbPort, Region)
                .WithKeySuffix()
                .Build();
            bool actualValue = dbMetastoreImpl.Store(TestKey, DateTimeOffset.Now, JObject.FromObject(keyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        private void TestStoreWithDbErrorShouldThrowException()
        {
            Dispose();
            Assert.Throws<AppEncryptionException>(() =>
                dynamoDbMetastoreImpl.Store(TestKey, DateTimeOffset.Now, JObject.FromObject(keyRecord)));
        }

        [Fact]
        private void TestStoreWithDuplicateShouldReturnFalse()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            bool firstAttempt = dynamoDbMetastoreImpl.Store(TestKey, now, JObject.FromObject(keyRecord));
            bool secondAttempt = dynamoDbMetastoreImpl.Store(TestKey, now, JObject.FromObject(keyRecord));

            Assert.True(firstAttempt);
            Assert.False(secondAttempt);
        }

        [Fact]
        private void TestPrimaryBuilderPath()
        {
            // Hack to inject default region since we don't explicitly require one be specified as we do in KMS impl
            AWSConfigs.AWSRegion = "us-west-2";
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        private void TestBuilderPathWithEndPointConfiguration()
        {
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithEndPointConfiguration("http://localhost:" + DynamoDbPort, Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        private void TestBuilderPathWithRegion()
        {
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithRegion("us-west-1")
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        private void TestBuilderPathWithKeySuffix()
        {
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithKeySuffix()
                .Build();

            Assert.NotNull(dbMetastoreImpl);
            Assert.True(dbMetastoreImpl.HasKeySuffix);
            Assert.False(dynamoDbMetastoreImpl.HasKeySuffix);
        }

        [Fact]
        private void TestBuilderPathWithTableName()
        {
            const string tempTableName = "DummyTable";

            // Use AWS SDK to create client
            AmazonDynamoDBConfig amazonDynamoDbConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000",
                AuthenticationRegion = "us-west-2",
            };
            AmazonDynamoDBClient tempDynamoDbClient = new AmazonDynamoDBClient(amazonDynamoDbConfig);
            CreateTableSchema(tempDynamoDbClient, tempTableName);

            // Put the object in temp table
            Table tempTable = Table.LoadTable(tempDynamoDbClient, tempTableName);
            JObject jObject = JObject.FromObject(keyRecord);
            Document document = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = created.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(jObject.ToString()),
            };
            tempTable.PutItemAsync(document).Wait();

            // Create a metastore object using the withTableName step
            DynamoDbMetastoreImpl dbMetastoreImpl = NewBuilder(Region)
                .WithEndPointConfiguration("http://localhost:" + DynamoDbPort, "us-west-2")
                .WithTableName(tempTableName)
                .Build();
            Option<JObject> actualJsonObject = dbMetastoreImpl.Load(TestKey, created);

            // Verify that we were able to load and successfully decrypt the item from the metastore object created withTableName
            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }
    }
}

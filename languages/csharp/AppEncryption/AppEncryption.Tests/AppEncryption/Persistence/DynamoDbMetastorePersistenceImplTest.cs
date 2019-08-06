using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Exceptions;
using LanguageExt;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.Persistence.DynamoDbMetastorePersistenceImpl;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class DynamoDbMetastorePersistenceImplTest : IClassFixture<DynamoDBContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
    {
        private const string TestKey = "some_key";

        private readonly AmazonDynamoDBClient amazonDynamoDbClient;

        private readonly Dictionary<string, object> keyRecord = new Dictionary<string, object>
        {
            {
                "ParentKeyMeta", new Dictionary<string, object>
                {
                    { "KeyId", "_SK_api_ecomm" },
                    { "Created", 1541461380 }
                }
            },
            { "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr" },
            { "Created", 1541461380 }
        };

        private readonly Dictionary<string, object> keyRecordLatest = new Dictionary<string, object>
        {
            {
                "ParentKeyMeta", new Dictionary<string, object>
                {
                    { "KeyId", "_SK_api_ecomm_latest" },
                    { "Created", 1541461380 }
                }
            },
            { "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr" },
            { "Created", 1541461380 }
        };

        private readonly Table table;
        private readonly DynamoDbMetastorePersistenceImpl dynamoDbMetastorePersistenceImpl;
        private readonly DateTimeOffset created = DateTimeOffset.Now.AddDays(-1);

        public DynamoDbMetastorePersistenceImplTest(DynamoDBContainerFixture dynamoDbContainerFixture)
        {
            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = dynamoDbContainerFixture.ServiceURL
            };
            amazonDynamoDbClient = new AmazonDynamoDBClient(clientConfig);

             CreateTableRequest request = new CreateTableRequest
                {
                    TableName = TableName,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition(PartitionKey, ScalarAttributeType.S),
                        new AttributeDefinition(SortKey, ScalarAttributeType.N)
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement(PartitionKey, KeyType.HASH),
                        new KeySchemaElement(SortKey, KeyType.RANGE)
                    },
                    ProvisionedThroughput = new ProvisionedThroughput(1L, 1L)
                };

            CreateTableResponse createTableResponse = amazonDynamoDbClient.CreateTableAsync(request).Result;
            table = Table.LoadTable(amazonDynamoDbClient, TableName);

            JObject jObject = JObject.FromObject(keyRecord);
            Document document = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = created.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(jObject.ToString())
            };

            Document result = table.PutItemAsync(document).Result;

            dynamoDbMetastorePersistenceImpl = new DynamoDbMetastorePersistenceImpl(amazonDynamoDbClient);
        }

        public void Dispose()
        {
            try
            {
                DeleteTableResponse deleteTableResponse = amazonDynamoDbClient.DeleteTableAsync(TableName).Result;
            }
            catch (AggregateException)
            {
                // There is no such table.
            }
        }

        [Fact]
        private void TestLoadSuccess()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.Load(TestKey, created);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        private void TestLoadWithNoResultShouldReturnEmpty()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.Load("fake_key", created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadWithFailureShouldReturnEmpty()
        {
            Dispose();
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.Load(TestKey, created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadLatestValueWithSingleRecord()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.LoadLatestValue(TestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(keyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        private void TestLoadLatestValueWithMultipleRecords()
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
                    { "mytime", createdPlusOneHour }
                }.ToString())
            };
            Document resultPlusOneHour = table.PutItemAsync(documentPlusOneHour).Result;

            Document documentPlusOneDay = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdPlusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdPlusOneDay }
                }.ToString())
            };
            Document resultPlusOneDay = table.PutItemAsync(documentPlusOneDay).Result;

            Document documentMinusOneHour = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdMinusOneHour.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneHour }
                }.ToString())
            };
            Document resultMinusOneHour = table.PutItemAsync(documentMinusOneHour).Result;

            Document documentMinusOneDay = new Document
            {
                [PartitionKey] = TestKey,
                [SortKey] = createdMinusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneDay }
                }.ToString())
            };
            Document resultMinusOneDay = table.PutItemAsync(documentMinusOneDay).Result;

            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.LoadLatestValue(TestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(createdPlusOneDay, ((JObject)actualJsonObject).GetValue("mytime")));
        }

        [Fact]
        private void TestLoadLatestValueWithNoResultShouldReturnEmpty()
        {
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.LoadLatestValue("fake_key");

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestLoadLatestValueWithFailureShouldReturnEmpty()
        {
            Dispose();
            Option<JObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.LoadLatestValue(TestKey);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        private void TestStore()
        {
            bool actualValue = dynamoDbMetastorePersistenceImpl.Store(TestKey, DateTimeOffset.Now, JObject.FromObject(keyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        private void TestStoreWithDbErrorShouldThrowException()
        {
            Dispose();
            Assert.Throws<AppEncryptionException>(() =>
                dynamoDbMetastorePersistenceImpl.Store(TestKey, DateTimeOffset.Now, JObject.FromObject(keyRecord)));
        }

        [Fact]
        private void TestStoreWithDuplicateShouldReturnFalse()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            bool firstAttempt = dynamoDbMetastorePersistenceImpl.Store(TestKey, now, JObject.FromObject(keyRecord));
            bool secondAttempt = dynamoDbMetastorePersistenceImpl.Store(TestKey, now, JObject.FromObject(keyRecord));

            Assert.True(firstAttempt);
            Assert.False(secondAttempt);
        }

        // This test is commented out since the constructor initializes the Table, which results in a network call. We decided
        // it wasn't worth the effort of refactoring it with thread-safe lazy loading for slightly higher code coverage.
//        [Fact]
//        private void TestPrimaryBuilderPath()
//        {
//            AWSConfigs.AWSRegion = "us-west-2";
//            Builder dynamoDbMetastorePersistenceServicePrimaryBuilder = NewBuilder();
//            DynamoDbMetastorePersistenceImpl dynamoDbMetastorePersistenceImpl =
//                dynamoDbMetastorePersistenceServicePrimaryBuilder.Build();
//            Assert.NotNull(dynamoDbMetastorePersistenceImpl);
//        }
    }
}

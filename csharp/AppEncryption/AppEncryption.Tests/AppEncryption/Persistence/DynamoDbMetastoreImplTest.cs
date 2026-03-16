using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Tests.Fixtures;
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [ExcludeFromCodeCoverage]
    public class DynamoDbMetastoreImplTest : IClassFixture<DynamoDbContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
    {
        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string AttributeKeyRecord = "KeyRecord";

        private const string Region = "us-west-2";

        private readonly AmazonDynamoDBClient _amazonDynamoDbClient;

        private readonly DynamoDbMetastoreImpl _dynamoDbMetastoreImpl;
        private readonly DateTimeOffset _created;
        private string _serviceUrl;

        public DynamoDbMetastoreImplTest(DynamoDbContainerFixture dynamoDbContainerFixture)
        {
            _serviceUrl = dynamoDbContainerFixture.GetServiceUrl();
            var clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = _serviceUrl,
                AuthenticationRegion = "us-west-2",
            };
            _amazonDynamoDbClient = new AmazonDynamoDBClient(clientConfig);

            DynamoDbMetastoreHelper.CreateTableSchema(_amazonDynamoDbClient, "EncryptionKey").Wait();

            _dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .Build();

            // Pre-populate test data using helper and capture the created timestamp
            _created = DynamoDbMetastoreHelper.PrePopulateTestDataUsingOldMetastore(_amazonDynamoDbClient, "EncryptionKey", Region).Result;
        }

        public void Dispose()
        {
            try
            {
                _ = _amazonDynamoDbClient.DeleteTableAsync(_dynamoDbMetastoreImpl.TableName).Result;
            }
            catch (AggregateException)
            {
                // There is no such table.
            }
        }


        [Fact]
        public void TestLoadSuccess()
        {
            var actualJsonObject = _dynamoDbMetastoreImpl.Load(DynamoDbMetastoreHelper.ExistingTestKey, _created);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        public void TestLoadWithNoResultShouldReturnEmpty()
        {
            var actualJsonObject = _dynamoDbMetastoreImpl.Load("fake_key", _created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        public void TestLoadWithFailureShouldReturnEmpty()
        {
            Dispose();
            var actualJsonObject = _dynamoDbMetastoreImpl.Load(DynamoDbMetastoreHelper.ExistingTestKey, _created);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        public void TestLoadLatestWithSingleRecord()
        {
            var actualJsonObject = _dynamoDbMetastoreImpl.LoadLatest(DynamoDbMetastoreHelper.ExistingTestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        public void TestLoadLatestWithSingleRecordAndSuffix()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithKeySuffix()
                .Build();

            var actualJsonObject = dbMetastoreImpl.LoadLatest(DynamoDbMetastoreHelper.ExistingTestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        public async Task TestLoadLatestWithMultipleRecords()
        {
            // Create a local table instance for this test
            var table = new TableBuilder(_amazonDynamoDbClient, _dynamoDbMetastoreImpl.TableName)
                .AddHashKey(PartitionKey, DynamoDBEntryType.String)
                .AddRangeKey(SortKey, DynamoDBEntryType.Numeric)
                .Build();

            var createdMinusOneHour = _created.AddHours(-1);
            var createdPlusOneHour = _created.AddHours(1);
            var createdMinusOneDay = _created.AddDays(-1);
            var createdPlusOneDay = _created.AddDays(1);

            // intentionally mixing up insertion order
            var documentPlusOneHour = new Document
            {
                [PartitionKey] = DynamoDbMetastoreHelper.ExistingTestKey,
                [SortKey] = createdPlusOneHour.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdPlusOneHour },
                }.ToString()),
            };
            await table.PutItemAsync(documentPlusOneHour, CancellationToken.None);

            var documentPlusOneDay = new Document
            {
                [PartitionKey] = DynamoDbMetastoreHelper.ExistingTestKey,
                [SortKey] = createdPlusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdPlusOneDay },
                }.ToString()),
            };
            await table.PutItemAsync(documentPlusOneDay, CancellationToken.None);

            var documentMinusOneHour = new Document
            {
                [PartitionKey] = DynamoDbMetastoreHelper.ExistingTestKey,
                [SortKey] = createdMinusOneHour.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneHour },
                }.ToString()),
            };
            await table.PutItemAsync(documentMinusOneHour, CancellationToken.None);

            var documentMinusOneDay = new Document
            {
                [PartitionKey] = DynamoDbMetastoreHelper.ExistingTestKey,
                [SortKey] = createdMinusOneDay.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(new JObject
                {
                    { "mytime", createdMinusOneDay },
                }.ToString()),
            };
            await table.PutItemAsync(documentMinusOneDay, CancellationToken.None);

            var actualJsonObject = _dynamoDbMetastoreImpl.LoadLatest(DynamoDbMetastoreHelper.ExistingTestKey);

            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(createdPlusOneDay, ((JObject)actualJsonObject).GetValue("mytime")));
        }

        [Fact]
        public void TestLoadLatestWithNoResultShouldReturnEmpty()
        {
            var actualJsonObject = _dynamoDbMetastoreImpl.LoadLatest("fake_key");

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        public void TestLoadLatestWithFailureShouldReturnEmpty()
        {
            Dispose();
            var actualJsonObject = _dynamoDbMetastoreImpl.LoadLatest(DynamoDbMetastoreHelper.ExistingTestKey);

            Assert.False(actualJsonObject.IsSome);
        }

        [Fact]
        public void TestStore()
        {
            var actualValue = _dynamoDbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, DateTimeOffset.Now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        public void TestStoreWithSuffixSuccess()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithKeySuffix()
                .Build();
            var actualValue = dbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, DateTimeOffset.Now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        public void TestStoreWithClientProvidedExternally()
        {
            var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
            {
                ServiceURL = _serviceUrl,
                AuthenticationRegion = Region,
            });

            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithDynamoDbClient(client)
                .Build();
            var actualValue = dbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, DateTimeOffset.Now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord));

            Assert.True(actualValue);
        }

        [Fact]
        public void TestStoreWithDbErrorShouldThrowException()
        {
            Dispose();
            Assert.Throws<AppEncryptionException>(() =>
                _dynamoDbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, DateTimeOffset.Now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord)));
        }

        [Fact]
        public void TestStoreWithDuplicateShouldReturnFalse()
        {
            var now = DateTimeOffset.Now;
            var firstAttempt = _dynamoDbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord));
            var secondAttempt = _dynamoDbMetastoreImpl.Store(DynamoDbMetastoreHelper.ExistingTestKey, now, JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord));

            Assert.True(firstAttempt);
            Assert.False(secondAttempt);
        }

        [Fact]
        public void TestBuilderPathWithEndPointConfiguration()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithRegion()
        {
            var builder = new Mock<DynamoDbMetastoreImpl.Builder>(Region);
            var loadTable = (Table)new TableBuilder(_amazonDynamoDbClient, "EncryptionKey")
                .AddHashKey(PartitionKey, DynamoDBEntryType.String)
                .AddRangeKey(SortKey, DynamoDBEntryType.Numeric)
                .Build();

            builder.Setup(x => x.LoadTable(It.IsAny<IAmazonDynamoDB>(), Region))
                .Returns(loadTable);

            var dbMetastoreImpl = builder.Object
                .WithRegion(Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithKeySuffix()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithKeySuffix()
                .Build();

            Assert.NotNull(dbMetastoreImpl);
            Assert.Equal(Region, dbMetastoreImpl.GetKeySuffix());
        }

        [Fact]
        public void TestBuilderPathWithoutKeySuffix()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
            Assert.Equal(string.Empty, dbMetastoreImpl.GetKeySuffix());
        }

        [Fact]
        public void TestBuilderPathWithCredentials()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithCredentials(new BasicAWSCredentials("dummykey", "dummy_secret"))
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithInvalidCredentials()
        {
            var emptySecretKey = string.Empty;
            Assert.ThrowsAny<Exception>(() => DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithCredentials(new BasicAWSCredentials("not-dummykey", emptySecretKey))
                .Build());
        }

        [Fact]
        public async Task TestBuilderPathWithTableName()
        {
            const string tempTableName = "DummyTable";

            // Use AWS SDK to create client
            var amazonDynamoDbConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = _serviceUrl,
                AuthenticationRegion = "us-west-2",
            };
            var tempDynamoDbClient = new AmazonDynamoDBClient(amazonDynamoDbConfig);
            await DynamoDbMetastoreHelper.CreateTableSchema(tempDynamoDbClient, tempTableName);

            // Put the object in temp table
            var tempTable = (Table)new TableBuilder(tempDynamoDbClient, tempTableName)
                .AddHashKey(PartitionKey, DynamoDBEntryType.String)
                .AddRangeKey(SortKey, DynamoDBEntryType.Numeric)
                .Build();
            var jObject = JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord);
            var document = new Document
            {
                [PartitionKey] = DynamoDbMetastoreHelper.ExistingTestKey,
                [SortKey] = _created.ToUnixTimeSeconds(),
                [AttributeKeyRecord] = Document.FromJson(jObject.ToString()),
            };
            await tempTable.PutItemAsync(document, CancellationToken.None);

            // Create a metastore object using the withTableName step
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, "us-west-2")
                .WithTableName(tempTableName)
                .Build();
            var actualJsonObject = dbMetastoreImpl.Load(DynamoDbMetastoreHelper.ExistingTestKey, _created);

            // Verify that we were able to load and successfully decrypt the item from the metastore object created withTableName
            Assert.True(actualJsonObject.IsSome);
            Assert.True(JToken.DeepEquals(JObject.FromObject(DynamoDbMetastoreHelper.ExistingKeyRecord), (JObject)actualJsonObject));
        }

        [Fact]
        public void TestPrimaryBuilderPath()
        {
            var builder = new Mock<DynamoDbMetastoreImpl.Builder>(Region);
            var loadTable = (Table)new TableBuilder(_amazonDynamoDbClient, "EncryptionKey")
                .AddHashKey(PartitionKey, DynamoDBEntryType.String)
                .AddRangeKey(SortKey, DynamoDBEntryType.Numeric)
                .Build();

            builder.Setup(x => x.LoadTable(It.IsAny<IAmazonDynamoDB>(), Region))
                .Returns(loadTable);

            var dbMetastoreImpl = builder.Object
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithLoggerEnabled()
        {
            var mockLogger = new Mock<ILogger>();

            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithLogger(mockLogger.Object)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithLoggerDisabled()
        {
            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestBuilderPathWithLoggerAndCredentials()
        {
            var mockLogger = new Mock<ILogger>();

            var dbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithLogger(mockLogger.Object)
                .WithCredentials(new BasicAWSCredentials("dummykey", "dummy_secret"))
                .Build();

            Assert.NotNull(dbMetastoreImpl);
        }

        [Fact]
        public void TestWithLoggerReturnsCorrectInterface()
        {
            var mockLogger = new Mock<ILogger>();

            var buildStep = DynamoDbMetastoreImpl.NewBuilder(Region)
                .WithEndPointConfiguration(_serviceUrl, Region)
                .WithLogger(mockLogger.Object);

            Assert.IsAssignableFrom<DynamoDbMetastoreImpl.IBuildStep>(buildStep);
        }
    }
}

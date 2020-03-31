using System;
using System.Data;
using System.Data.Common;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Exceptions;
using LanguageExt;
using Moq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.Persistence.AdoMetastoreImpl;

// TODO Verify that the DbCommand gets closed.
namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class AdoMetastoreImplTest : IClassFixture<MySqlContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
    {
        private const string KeyStringWithParentKeyMetaKey = "key_with_parentkeymeta";

        private const string KeyStringWithParentKeyMetaValue = "{\"ParentKeyMeta\":{\"KeyId\":\"_SK_api_ecomm\"," +
                                                               "\"Created\":1541461380}," +
                                                               "\"Key\":\"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr\",\"Created\":1541461380}";

        private const string KeyStringLatestValue = "{\"ParentKeyMeta\":{\"KeyId\":\"_SK_api_ecomm_latest\"," +
                                                    "\"Created\":1541461380}," +
                                                    "\"Key\":\"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr\",\"Created\":1541461380}";

        private const string KeyStringWithNoParentKeyMetaKey = "key_with_no_parentkeymeta";

        private const string KeyStringWithNoParentKeyMetaValue =
            "{\"Key\":\"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr\",\"Created\":1541461380}";

        private const string MalformedKeyStringKey = "malformed_key_string";
        private const string MalformedKeyStringValue = "{\"ParentKeyMeta\":{\"KeyId\"";

        private readonly DateTimeOffset created = new DateTimeOffset(2019, 1, 1, 23, 0, 0, TimeSpan.Zero);

        private readonly DbProviderFactory dbProviderFactory;
        private readonly DbConnection dbConnection;
        private readonly Mock<AdoMetastoreImpl> adoMetastoreImplSpy;
        private readonly string connectionString;

        // Create a connection string with incorrect user id. This is used to force generate a DbException while setting up a connection
        private readonly DbConnectionStringBuilder fakeDbConnectionStringBuilder = new DbConnectionStringBuilder
        {
            ["server"] = "localhost", ["user id"] = "some_id_",
        };

        public AdoMetastoreImplTest(MySqlContainerFixture fixture)
        {
            dbProviderFactory = MySqlClientFactory.Instance;
            connectionString = fixture.ConnectionString + "Initial Catalog=testdb;";
            dbConnection = dbProviderFactory.CreateConnection();
            dbConnection.ConnectionString = fixture.ConnectionString;
            dbConnection.Open();

            adoMetastoreImplSpy = new Mock<AdoMetastoreImpl>(
                dbProviderFactory,
                connectionString) { CallBase = true };
            SetupDatabase();
        }

        public void Dispose()
        {
            using (DbConnection dbConnectionForTearDown = dbProviderFactory.CreateConnection())
            {
                dbConnectionForTearDown.ConnectionString = connectionString;
                dbConnectionForTearDown.Open();

                using (DbCommand dbCommand = dbConnectionForTearDown.CreateCommand())
                {
                    string createDatabaseQuery = @"DROP DATABASE testdb;";
                    dbCommand.CommandText = createDatabaseQuery;
                    dbCommand.ExecuteNonQuery();
                }
            }

            dbConnection.Close();
        }

        private void SetupDatabase()
        {
            using (DbCommand dbCommand = dbConnection.CreateCommand())
            {
                string createDatabaseQuery = @"CREATE DATABASE testdb;";
                dbCommand.CommandText = createDatabaseQuery;
                dbCommand.ExecuteNonQuery();

                string createTableQuery =
                    @"CREATE TABLE testdb.encryption_key
                (
                    id VARCHAR(255) NOT NULL,
                    created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    key_record TEXT NOT NULL,
                    PRIMARY KEY (id, created),
                    INDEX (created)
                );";
                dbCommand.CommandText = createTableQuery;
                dbCommand.ExecuteNonQuery();

                string insertDataQuery =
                    @"INSERT INTO testdb.encryption_key (id, created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery;
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithParentKeyMetaKey);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringWithParentKeyMetaValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery1 =
                    @"INSERT INTO testdb.encryption_key (id ,created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery1;
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithNoParentKeyMetaKey);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringWithNoParentKeyMetaValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery2 =
                    @"INSERT INTO testdb.encryption_key (id ,created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery2;
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Id, MalformedKeyStringKey);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, KeyRecord, MalformedKeyStringValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery4 =
                    @"INSERT INTO testdb.encryption_key (id, created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery4;

                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithParentKeyMetaKey);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, Created, created.AddHours(1).UtcDateTime);
                adoMetastoreImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringLatestValue);
                dbCommand.ExecuteNonQuery();
                Console.WriteLine("Starting test");
            }
        }

        [Theory]
        [InlineData(KeyStringWithParentKeyMetaKey, KeyStringWithParentKeyMetaValue)]
        [InlineData(KeyStringWithNoParentKeyMetaKey, KeyStringWithNoParentKeyMetaValue)]
        private void TestExecuteQueryAndLoadJsonObject(string keyStringKey, string keyStringValue)
        {
            using (DbCommand command = dbConnection.CreateCommand())
            {
                string selectQuery =
                    @"SELECT key_record from  testdb.encryption_key WHERE id=@id AND created=@created;";
                command.CommandText = selectQuery;
                adoMetastoreImplSpy.Object.AddParameter(command, Id, keyStringKey);
                adoMetastoreImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualJsonObject =
                    adoMetastoreImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);
                Assert.True(actualJsonObject.IsSome);
                Assert.Equal(keyStringValue, ((JObject)actualJsonObject).ToString(Formatting.None));
            }
        }

        [Fact]
        private void TestExecuteQueryAndLoadJsonObjectFromKeyWithNoResultShouldReturnNone()
        {
            using (DbCommand command = dbConnection.CreateCommand())
            {
                string selectQuery =
                    @"SELECT key_record from  testdb.encryption_key WHERE id=@id And created=@created;";
                command.CommandText = selectQuery;
                adoMetastoreImplSpy.Object.AddParameter(command, Id, "non_existent_key");
                adoMetastoreImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualJsonObject =
                    adoMetastoreImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);
                Assert.Equal(Option<JObject>.None, actualJsonObject);
            }
        }

        [Fact]
        private void TestExecuteQueryAndLoadJsonObjectFromKeyWithExceptionShouldReturnNone()
        {
            using (DbCommand command = dbConnection.CreateCommand())
            {
                string selectQuery =
                    @"SELECT key_record from  testdb.encryption_key WHERE id=@id And created=@created;";
                command.CommandText = selectQuery;
                adoMetastoreImplSpy.Object.AddParameter(command, Id, MalformedKeyStringKey);
                adoMetastoreImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualValue =
                    adoMetastoreImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);

                Assert.Equal(Option<JObject>.None, actualValue);
            }
        }

        [Fact]
        private void TestLoad()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastoreImplSpy.Object.Load(keyId, created.UtcDateTime);
            Assert.True(actualJsonObject.IsSome);
            Assert.Equal(KeyStringWithParentKeyMetaValue, ((JObject)actualJsonObject).ToString(Formatting.None));
        }

        [Fact]
        private void TestLoadWithSqlException()
        {
            AdoMetastoreImpl adoMetastoreImpl = new AdoMetastoreImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastoreImpl.Load(keyId, created.UtcDateTime);
            Assert.Equal(Option<JObject>.None, actualJsonObject);
        }

        [Fact]
        private void TestLoadLatest()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastoreImplSpy.Object.LoadLatest(keyId);
            Assert.True(actualJsonObject.IsSome);
            Assert.Equal(KeyStringLatestValue, ((JObject)actualJsonObject).ToString(Formatting.None));
        }

        [Fact]
        private void TestLoadLatestWithSqlException()
        {
            AdoMetastoreImpl adoMetastoreImpl = new AdoMetastoreImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastoreImpl.LoadLatest(keyId);
            Assert.Equal(Option<JObject>.None, actualJsonObject);
        }

        [Fact]
        private void TestStore()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            bool actualValue = adoMetastoreImplSpy.Object.Store(
                keyId,
                DateTimeOffset.UtcNow,
                JObject.Parse(KeyStringLatestValue));
            Assert.True(actualValue);
        }

        [Fact]
        private void TestStoreWithSqlExceptionShouldReturnFalse()
        {
            AdoMetastoreImpl adoMetastoreImpl = new AdoMetastoreImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            bool actualValue = adoMetastoreImpl.Store(keyId, DateTimeOffset.UtcNow, new JObject());
            Assert.False(actualValue);
        }

        [Fact]
        private void TestStoreWithDuplicateKeyInsertionShouldReturnFalse()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            bool actualValue = adoMetastoreImplSpy.Object.Store(
                keyId,
                created.UtcDateTime,
                JObject.Parse(KeyStringWithParentKeyMetaValue));
            Assert.False(actualValue);
        }

        [Fact]
        private void TestPrimaryBuilderPath()
        {
            AdoMetastoreImpl.Builder adoMetastoreServicePrimaryBuilder =
                NewBuilder(dbProviderFactory, dbConnection.ConnectionString);
            AdoMetastoreImpl adoMetastoreServiceBuilder =
                adoMetastoreServicePrimaryBuilder.Build();
            Assert.NotNull(adoMetastoreServiceBuilder);
        }

        [Fact]
        private void TestDbConnectionClosedAfterLoad()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastoreImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastoreImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            adoMetastoreImplSpy.Setup(x => x.ExecuteQueryAndLoadJsonObjectFromKey(dbCommandMock.Object))
                .Returns(Option<JObject>.None);
            adoMetastoreImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();
            adoMetastoreImplSpy.Object.Load(KeyStringWithParentKeyMetaKey, created.UtcDateTime);

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }

        [Fact]
        private void TestDbConnectionClosedAfterLoadLatest()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastoreImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastoreImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            adoMetastoreImplSpy.Setup(x => x.ExecuteQueryAndLoadJsonObjectFromKey(dbCommandMock.Object))
                .Returns(Option<JObject>.None);
            adoMetastoreImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();
            adoMetastoreImplSpy.Object.LoadLatest(KeyStringWithParentKeyMetaKey);

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }

        [Fact]
        private void TestDbConnectionClosedAfterStore()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastoreImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastoreImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            dbCommandMock.Setup(x => x.ExecuteNonQuery()).Returns(1);
            adoMetastoreImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();

            adoMetastoreImplSpy.Object.Store(
                KeyStringWithNoParentKeyMetaKey,
                created.UtcDateTime,
                JObject.Parse(KeyStringWithNoParentKeyMetaValue));

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }
    }
}

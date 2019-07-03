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
using static GoDaddy.Asherah.AppEncryption.Persistence.AdoMetastorePersistenceImpl;

// TODO Verify that the DbCommand gets closed.
namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class AdoMetastorePersistenceImplTest : IClassFixture<MySqlContainerFixture>, IClassFixture<MetricsFixture>, IDisposable
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
        private readonly Mock<AdoMetastorePersistenceImpl> adoMetastorePersistenceImplSpy;
        private readonly string connectionString;

        // Create a connection string with incorrect user id. This is used to force generate a DbException while setting up a connection
        private readonly DbConnectionStringBuilder fakeDbConnectionStringBuilder = new DbConnectionStringBuilder
        {
            ["server"] = "localhost", ["user id"] = "some_id_"
        };

        public AdoMetastorePersistenceImplTest(MySqlContainerFixture fixture)
        {
            dbProviderFactory = MySqlClientFactory.Instance;
            connectionString = fixture.ConnectionString + "Initial Catalog=testdb;";
            dbConnection = dbProviderFactory.CreateConnection();
            dbConnection.ConnectionString = fixture.ConnectionString;
            dbConnection.Open();

            adoMetastorePersistenceImplSpy = new Mock<AdoMetastorePersistenceImpl>(
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
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithParentKeyMetaKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringWithParentKeyMetaValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery1 =
                    @"INSERT INTO testdb.encryption_key (id ,created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery1;
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithNoParentKeyMetaKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringWithNoParentKeyMetaValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery2 =
                    @"INSERT INTO testdb.encryption_key (id ,created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery2;
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Id, MalformedKeyStringKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Created, created.UtcDateTime);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, KeyRecord, MalformedKeyStringValue);
                dbCommand.ExecuteNonQuery();
                dbCommand.Parameters.Clear();

                string insertDataQuery4 =
                    @"INSERT INTO testdb.encryption_key (id, created, key_record) VALUES(@id, @created, @key_record);";
                dbCommand.CommandText = insertDataQuery4;

                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Id, KeyStringWithParentKeyMetaKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, Created, created.AddHours(1).UtcDateTime);
                adoMetastorePersistenceImplSpy.Object.AddParameter(dbCommand, KeyRecord, KeyStringLatestValue);
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
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Id, keyStringKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualJsonObject =
                    adoMetastorePersistenceImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);
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
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Id, "non_existent_key");
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualJsonObject =
                    adoMetastorePersistenceImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);
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
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Id, MalformedKeyStringKey);
                adoMetastorePersistenceImplSpy.Object.AddParameter(command, Created, created.UtcDateTime);

                Option<JObject> actualValue =
                    adoMetastorePersistenceImplSpy.Object.ExecuteQueryAndLoadJsonObjectFromKey(command);

                Assert.Equal(Option<JObject>.None, actualValue);
            }
        }

        [Fact]
        private void TestLoad()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastorePersistenceImplSpy.Object.Load(keyId, created.UtcDateTime);
            Assert.True(actualJsonObject.IsSome);
            Assert.Equal(KeyStringWithParentKeyMetaValue, ((JObject)actualJsonObject).ToString(Formatting.None));
        }

        [Fact]
        private void TestLoadWithSqlException()
        {
            AdoMetastorePersistenceImpl adoMetastorePersistenceImpl = new AdoMetastorePersistenceImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastorePersistenceImpl.Load(keyId, created.UtcDateTime);
            Assert.Equal(Option<JObject>.None, actualJsonObject);
        }

        [Fact]
        private void TestLoadLatestValue()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastorePersistenceImplSpy.Object.LoadLatestValue(keyId);
            Assert.True(actualJsonObject.IsSome);
            Assert.Equal(KeyStringLatestValue, ((JObject)actualJsonObject).ToString(Formatting.None));
        }

        [Fact]
        private void TestLoadLatestValueWithSqlException()
        {
            AdoMetastorePersistenceImpl adoMetastorePersistenceImpl = new AdoMetastorePersistenceImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            Option<JObject> actualJsonObject = adoMetastorePersistenceImpl.LoadLatestValue(keyId);
            Assert.Equal(Option<JObject>.None, actualJsonObject);
        }

        [Fact]
        private void TestStore()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            bool actualValue = adoMetastorePersistenceImplSpy.Object.Store(
                keyId,
                DateTimeOffset.UtcNow,
                JObject.Parse(KeyStringLatestValue));
            Assert.True(actualValue);
        }

        [Fact]
        private void TestStoreWithSqlException()
        {
            AdoMetastorePersistenceImpl adoMetastorePersistenceImpl = new AdoMetastorePersistenceImpl(
                dbProviderFactory,
                fakeDbConnectionStringBuilder.ConnectionString);
            string keyId = KeyStringWithParentKeyMetaKey;
            Assert.Throws<AppEncryptionException>(() =>
                adoMetastorePersistenceImpl.Store(keyId, DateTimeOffset.UtcNow, new JObject()));
        }

        [Fact]
        private void TestStoreWithDuplicateKeyInsertionShouldReturnFalse()
        {
            string keyId = KeyStringWithParentKeyMetaKey;
            bool actualValue = adoMetastorePersistenceImplSpy.Object.Store(
                keyId,
                created.UtcDateTime,
                JObject.Parse(KeyStringWithParentKeyMetaValue));
            Assert.False(actualValue);
        }

        [Fact]
        private void TestPrimaryBuilderPath()
        {
            AdoMetastorePersistenceImpl.Builder adoMetastorePersistenceServicePrimaryBuilder =
                NewBuilder(dbProviderFactory, dbConnection.ConnectionString);
            AdoMetastorePersistenceImpl adoMetastorePersistenceServiceBuilder =
                adoMetastorePersistenceServicePrimaryBuilder.Build();
            Assert.NotNull(adoMetastorePersistenceServiceBuilder);
        }

        [Fact]
        private void TestDbConnectionClosedAfterLoad()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastorePersistenceImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastorePersistenceImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            adoMetastorePersistenceImplSpy.Setup(x => x.ExecuteQueryAndLoadJsonObjectFromKey(dbCommandMock.Object))
                .Returns(Option<JObject>.None);
            adoMetastorePersistenceImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();
            adoMetastorePersistenceImplSpy.Object.Load(KeyStringWithParentKeyMetaKey, created.UtcDateTime);

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }

        [Fact]
        private void TestDbConnectionClosedAfterLoadLatestValue()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastorePersistenceImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastorePersistenceImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            adoMetastorePersistenceImplSpy.Setup(x => x.ExecuteQueryAndLoadJsonObjectFromKey(dbCommandMock.Object))
                .Returns(Option<JObject>.None);
            adoMetastorePersistenceImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();
            adoMetastorePersistenceImplSpy.Object.LoadLatestValue(KeyStringWithParentKeyMetaKey);

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }

        [Fact]
        private void TestDbConnectionClosedAfterStore()
        {
            Mock<DbCommand> dbCommandMock = new Mock<DbCommand>();
            adoMetastorePersistenceImplSpy.Setup(x => x.CreateCommand(It.IsAny<DbConnection>()))
                .Returns(dbCommandMock.Object);
            adoMetastorePersistenceImplSpy.Setup(x => x.GetConnection()).Returns(dbConnection);

            Assert.Equal(ConnectionState.Open, dbConnection.State);

            dbCommandMock.Setup(x => x.ExecuteNonQuery()).Returns(1);
            adoMetastorePersistenceImplSpy
                .Setup(x => x.AddParameter(It.IsAny<DbCommand>(), It.IsAny<string>(), It.IsAny<object>())).Verifiable();

            adoMetastorePersistenceImplSpy.Object.Store(
                KeyStringWithNoParentKeyMetaKey,
                created.UtcDateTime,
                JObject.Parse(KeyStringWithNoParentKeyMetaValue));

            // Verify that DbConnection is closed at the end of the function call
            Assert.Equal(ConnectionState.Closed, dbConnection.State);
        }
    }
}

using System;
using System.Data.Common;
using System.Runtime.CompilerServices;
using App.Metrics.Timer;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("AppEncryption.Tests")]

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    /// <summary>
    /// Provides an Ado based implementation of <see cref="IMetastore{T}"/> to store and retrieve system keys and
    /// intermediate keys as <see cref="JObject"/> values. These keys are used by Asherah to provide
    /// a hierarchical key structure. It uses the table name "encryption_key" to perform all RDBMS based operations.
    /// Stores the created time in UTC.
    /// </summary>
    public class AdoMetastoreImpl : IMetastore<JObject>
    {
        internal const string Created = "created";
        internal const string Id = "id";
        internal const string KeyRecord = "key_record";

        private const string LoadQuery = @"SELECT key_record from encryption_key where id = @id and created = @created";

        private const string StoreQuery =
            @"INSERT INTO encryption_key (id, created, key_record) VALUES (@id, @created, @key_record)";

        private const string LoadLatestQuery =
            @"SELECT key_record from encryption_key where id = @id order by created DESC limit 1";

        private static readonly ILogger Logger = LogManager.CreateLogger<AdoMetastoreImpl>();

        private static readonly TimerOptions LoadTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.ado.load" };
        private static readonly TimerOptions LoadLatestTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.ado.loadlatest" };
        private static readonly TimerOptions StoreTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.ado.store" };

        private readonly string connectionString;
        private readonly DbProviderFactory dbProviderFactory;

        internal AdoMetastoreImpl(DbProviderFactory dbProviderFactory, string connectionString)
        {
            this.connectionString = connectionString;
            this.dbProviderFactory = dbProviderFactory;
        }

        /// <summary>
        /// Initializes a <see cref="AdoMetastoreImpl"/> builder using the provided parameters.
        /// </summary>
        ///
        /// <param name="dbProviderFactory">The <seealso cref="DbProviderFactory"/> object which represents a set of
        /// methods for creating instances of a provider's implementation of the data source classes.</param>
        /// <param name="connectionString">The connection string to be used by the <seealso cref="DbConnection"/>.
        /// </param>
        /// <returns>The current <see cref="Builder"/> step.</returns>
        public static Builder NewBuilder(DbProviderFactory dbProviderFactory, string connectionString)
        {
            return new Builder(dbProviderFactory, connectionString);
        }

        /// <summary>
        /// Uses the <see cref="LoadQuery"/> to retrieve the value associated with the keyId and time it was created.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <param name="created">The created time to lookup which is converted to UTC.</param>
        /// <returns>The <seealso cref="JObject"/> value associated with the <paramref name="keyId"/> and
        /// <paramref name="created"/> tuple.</returns>
        public virtual Option<JObject> Load(string keyId, DateTimeOffset created)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadTimerOptions))
            {
                try
                {
                    using (DbConnection connection = GetConnection())
                    {
                        using (DbCommand command = CreateCommand(connection))
                        {
                            command.CommandText = LoadQuery;
                            AddParameter(command, Id, keyId);
                            AddParameter(command, Created, created.UtcDateTime);

                            return ExecuteQueryAndLoadJsonObjectFromKey(command);
                        }
                    }
                }
                catch (DbException dbe)
                {
                    Logger.LogError(dbe, "Metastore error");
                }

                return Option<JObject>.None;
            }
        }

        /// <summary>
        /// Uses the <see cref="LoadLatestQuery"/> to lookup the latest value associated with the keyId.
        /// </summary>
        ///
        /// <param name="keyId">the keyId to lookup.</param>
        /// <returns>The latest value associated with the keyId, if any.</returns>
        public Option<JObject> LoadLatest(string keyId)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadLatestTimerOptions))
            {
                try
                {
                    using (DbConnection connection = GetConnection())
                    {
                        using (DbCommand command = CreateCommand(connection))
                        {
                            command.CommandText = LoadLatestQuery;
                            AddParameter(command, Id, keyId);

                            return ExecuteQueryAndLoadJsonObjectFromKey(command);
                        }
                    }
                }
                catch (DbException dbe)
                {
                    Logger.LogError(dbe, "Metastore error");
                }

                return Option<JObject>.None;
            }
        }

        /// <summary>
        /// Uses the <seealso cref="StoreQuery"/> to store the value using the specified keyId and created time.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to store.</param>
        /// <param name="created">The created time to store.</param>
        /// <param name="value">The <seealso cref="JObject"/> value to store.</param>
        /// <returns><value>True</value> if the store succeeded, false if the store failed for a known condition
        /// e.g., trying to save a duplicate value should return false, not throw an exception. </returns>
        public bool Store(string keyId, DateTimeOffset created, JObject value)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(StoreTimerOptions))
            {
                try
                {
                    using (DbConnection connection = GetConnection())
                    {
                        using (DbCommand command = CreateCommand(connection))
                        {
                            command.CommandText = StoreQuery;
                            AddParameter(command, Id, keyId);
                            AddParameter(command, Created, created.UtcDateTime);
                            AddParameter(command, KeyRecord, value.ToString(Formatting.None));

                            int result = command.ExecuteNonQuery();

                            return result == 1;
                        }
                    }
                }
                catch (DbException dbe)
                {
                    Logger.LogError(dbe, "Metastore error during store");

                    // ADO based persistence does not provide any kind of specific integrity violation error
                    // code/exception. Hence we always return false even for systemic issues to keep things simple.
                    return false;
                }
            }
        }

        internal virtual void AddParameter(DbCommand command, string name, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        internal virtual Option<JObject> ExecuteQueryAndLoadJsonObjectFromKey(DbCommand command)
        {
            using (DbDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    string keyString = reader.GetString(reader.GetOrdinal(KeyRecord));

                    try
                    {
                        return Option<JObject>.Some(JObject.Parse(keyString));
                    }
                    catch (JsonException e)
                    {
                        Logger.LogError(e, "Failed to create JSON from key");
                    }
                }
            }

            return Option<JObject>.None;
        }

        internal virtual DbConnection GetConnection()
        {
            DbConnection connection = dbProviderFactory.CreateConnection();
            connection.ConnectionString = connectionString;
            connection.Open();
            return connection;
        }

        internal virtual DbCommand CreateCommand(DbConnection connection)
        {
            return connection.CreateCommand();
        }

        /// <summary>
        /// Builder class to create an instance of the <see cref="AdoMetastoreImpl"/> class.
        /// </summary>
        public class Builder
        {
            private readonly DbProviderFactory dbProviderFactory;
            private readonly string connectionString;

            internal Builder(DbProviderFactory dbProviderFactory, string connectionString)
            {
                this.dbProviderFactory = dbProviderFactory;
                this.connectionString = connectionString;
            }

            /// <summary>
            /// Builds the finalized <see cref="AdoMetastoreImpl"/> object with the parameters specified in the
            /// <see cref="Builder"/>.
            /// </summary>
            ///
            /// <returns>The fully instantiated <see cref="AdoMetastoreImpl"/> object.</returns>
            public AdoMetastoreImpl Build()
            {
                return new AdoMetastoreImpl(dbProviderFactory, connectionString);
            }
        }
    }
}

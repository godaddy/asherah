using System;
using System.Data.Common;
using System.Runtime.CompilerServices;
using App.Metrics.Timer;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Logging;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("AppEncryption.Tests")]

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
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

        public AdoMetastoreImpl(DbProviderFactory dbProviderFactory, string connectionString)
        {
            this.connectionString = connectionString;
            this.dbProviderFactory = dbProviderFactory;
        }

        public static Builder NewBuilder(DbProviderFactory dbProviderFactory, string connectionString)
        {
            return new Builder(dbProviderFactory, connectionString);
        }

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

        public class Builder
        {
            private readonly DbProviderFactory dbProviderFactory;
            private readonly string connectionString;

            internal Builder(DbProviderFactory dbProviderFactory, string connectionString)
            {
                this.dbProviderFactory = dbProviderFactory;
                this.connectionString = connectionString;
            }

            public AdoMetastoreImpl Build()
            {
                return new AdoMetastoreImpl(dbProviderFactory, connectionString);
            }
        }
    }
}

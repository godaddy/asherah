using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using App.Metrics.Timer;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Logging;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("AppEncryption.Tests")]

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    public class DynamoDbMetastoreImpl : IMetastore<JObject>
    {
        internal const string PartitionKey = "Id";
        internal const string SortKey = "Created";
        internal const string AttributeKeyRecord = "KeyRecord";

        private static readonly TimerOptions LoadTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.load" };
        private static readonly TimerOptions LoadLatestTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.loadlatest" };
        private static readonly TimerOptions StoreTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.store" };

        private static readonly ILogger Logger = LogManager.CreateLogger<DynamoDbMetastoreImpl>();

        private readonly IAmazonDynamoDB dbClient;
        private readonly string keySuffix;
        private readonly string tableName;

        private DynamoDbMetastoreImpl(Builder builder)
        {
            dbClient = builder.DbClient;
            keySuffix = builder.KeySuffix;
            tableName = builder.TableName;
        }

        public interface IBuildStep
        {
            /// <summary>
            /// Specifies whether key suffix should be enabled for DynamoDB.
            /// </summary>
            /// <param name="suffix">the region to be used as suffix.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithKeySuffix(string suffix);

            /// <summary>
            /// Specifies the name of the table.
            /// </summary>
            /// <param name="tableName">The name of the table.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithTableName(string tableName);

            /// <summary>
            /// Builds the finalized <code>DynamoDbMetastoreImpl</code> with the parameters specified in the builder.
            /// </summary>
            /// <returns>The fully instantiated <code>DynamoDbMetastoreImpl</code>.</returns>
            DynamoDbMetastoreImpl Build();
        }

        private interface IEndPointStep
        {
            /// <summary>
            /// Adds EndPoint config to the AWS DynamoDb client.
            /// </summary>
            /// <param name="endPoint">the service endpoint either with or without the protocol.</param>
            /// <param name="signingRegion">the region to use for SigV4 signing of requests (e.g. us-west-1).</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithEndPointConfiguration(string endPoint, string signingRegion);
        }

        private interface IRegionStep
        {
            /// <summary>
            /// Specifies the region for the AWS DynamoDb client.
            /// </summary>
            /// <param name="region">The region for the DynamoDb client.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithRegion(string region);
        }

        public static Builder NewBuilder()
        {
            return new Builder();
        }

        public Option<JObject> Load(string keyId, DateTimeOffset created)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadTimerOptions))
            {
                try
                {
                    Table table = Table.LoadTable(dbClient, tableName);
                    GetItemOperationConfig config = new GetItemOperationConfig
                    {
                        AttributesToGet = new List<string> { AttributeKeyRecord },
                        ConsistentRead = true, // Always use strong consistency
                    };
                    Document result = table.GetItemAsync(keyId, created.ToUnixTimeSeconds(), config).Result;
                    if (result != null)
                    {
                        // TODO Optimize Document to JObject conversion. Helper method could enumerate over Document KeyPairs
                        // and convert DynamoDBEntry values based on type inspection
                        return Option<JObject>.Some(JObject.Parse(result[AttributeKeyRecord].AsDocument().ToJson()));
                    }
                }
                catch (AggregateException ae)
                {
                    Logger.LogError(ae, "Metastore error");
                }

                return Option<JObject>.None;
            }
        }

        public Option<JObject> LoadLatest(string keyId)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadLatestTimerOptions))
            {
                // Have to use query api to use limit and reverse sort order
                try
                {
                    Table table = Table.LoadTable(dbClient, tableName);
                    QueryFilter filter = new QueryFilter(PartitionKey, QueryOperator.Equal, keyId);
                    QueryOperationConfig config = new QueryOperationConfig
                    {
                        Limit = 1,
                        ConsistentRead = true, // always use strong consistency
                        BackwardSearch = true, // sorts descending
                        Filter = filter,
                        AttributesToGet = new List<string> { AttributeKeyRecord },
                        Select = SelectValues.SpecificAttributes,
                    };
                    Search search = table.Query(config);
                    List<Document> result = search.GetNextSetAsync().Result;
                    if (result.Count > 0)
                    {
                        Document keyRecordDocument = result.First();

                        // TODO Optimize Document to JObject conversion. Helper method could enumerate over Document KeyPairs
                        // and convert DynamoDBEntry values based on type inspection
                        return Option<JObject>.Some(JObject.Parse(keyRecordDocument[AttributeKeyRecord].AsDocument().ToJson()));
                    }
                }
                catch (AggregateException se)
                {
                    Logger.LogError(se, "Metastore error");
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
                    Table table = Table.LoadTable(dbClient, tableName);
                    Document document = new Document
                    {
                        [PartitionKey] = keyId,
                        [SortKey] = created.ToUnixTimeSeconds(),

                        // TODO Optimize JObject to Document conversion. Just need lambda that calls Document.Add and recurses
                        // for Dictionary and List types
                        [AttributeKeyRecord] = Document.FromJson(value.ToString()),
                    };

                    // Note conditional expression using attribute_not_exists has special semantics. Can be used on partition OR sort key
                    // alone to guarantee primary key uniqueness. It automatically checks for existence of this item's composite primary key
                    // and if it contains the specified attribute name, either of which is inherently required.
                    Expression expr = new Expression
                        { ExpressionStatement = "attribute_not_exists(" + PartitionKey + ")" };
                    PutItemOperationConfig config = new PutItemOperationConfig
                    {
                        ConditionalExpression = expr,
                    };

                    // This a blocking call using Result because we need to wait for the call to complete before proceeding
                    Document result = table.PutItemAsync(document, config).Result;
                    return true;
                }
                catch (AggregateException ae)
                {
                    foreach (Exception exception in ae.InnerExceptions)
                    {
                        if (exception is ConditionalCheckFailedException)
                        {
                            Logger.LogInformation("Attempted to create duplicate key: {keyId} {created}", keyId, created);
                            return false;
                        }
                    }

                    Logger.LogError(ae, "Metastore error during store");
                    throw new AppEncryptionException("Metastore error:", ae);
                }
            }
        }

        public IAmazonDynamoDB GetClient()
        {
            return dbClient;
        }

        public string GetKeySuffix()
        {
            return keySuffix;
        }

        public string GetTableName()
        {
            return tableName;
        }

        public class Builder : IBuildStep, IEndPointStep, IRegionStep
        {
            #pragma warning disable SA1401
            internal IAmazonDynamoDB DbClient;
            internal string KeySuffix = DefaultKeySuffix;
            internal string TableName = DefaultTableName;
            #pragma warning restore SA1401

            private const string DefaultTableName = "EncryptionKey";
            private static readonly string DefaultKeySuffix = string.Empty;
            private readonly AmazonDynamoDBConfig dbConfig = new AmazonDynamoDBConfig();

            public IBuildStep WithKeySuffix(string suffix)
            {
                KeySuffix = suffix;
                return this;
            }

            public IBuildStep WithTableName(string tableName)
            {
                TableName = tableName;
                return this;
            }

            public IBuildStep WithEndPointConfiguration(string endPoint, string signingRegion)
            {
                dbConfig.ServiceURL = endPoint;
                dbConfig.AuthenticationRegion = signingRegion;
                return this;
            }

            public IBuildStep WithRegion(string region)
            {
                dbConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
                return this;
            }

            public DynamoDbMetastoreImpl Build()
            {
                DbClient = new AmazonDynamoDBClient(dbConfig);
                return new DynamoDbMetastoreImpl(this);
            }
        }
    }
}

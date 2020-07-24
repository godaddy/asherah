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
    /// <summary>
    /// Provides an AWS DynamoDB based implementation of <see cref="IMetastore{T}"/> to store and retrieve system keys
    /// and intermediate keys as <see cref="JObject"/> valyes. These are used by Asherah to provide a hierarchical key
    /// structure. It uses the default table name "EncryptionKey" but it can be configured using
    /// <see cref="IBuildStep.WithTableName"/> option. Stores the created time in unix time seconds.
    /// </summary>
    public class DynamoDbMetastoreImpl : IMetastore<JObject>
    {
        internal const string PartitionKey = "Id";
        internal const string SortKey = "Created";
        internal const string AttributeKeyRecord = "KeyRecord";

        private static readonly TimerOptions LoadTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.load" };
        private static readonly TimerOptions LoadLatestTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.loadlatest" };
        private static readonly TimerOptions StoreTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.store" };

        private static readonly ILogger Logger = LogManager.CreateLogger<DynamoDbMetastoreImpl>();

        private readonly string preferredRegion;
        private readonly Table table;

        private DynamoDbMetastoreImpl(Builder builder)
        {
            DbClient = builder.DbClient;
            TableName = builder.TableName;
            preferredRegion = builder.PreferredRegion;
            HasKeySuffix = builder.HasKeySuffix;
            Table.TryLoadTable(DbClient, TableName, out table);
        }

        public interface IBuildStep
        {
            /// <summary>
            /// Specifies whether key suffix should be enabled for DynamoDB.
            /// </summary>
            ///
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithKeySuffix();

            /// <summary>
            /// Used to define a custom table name in DynamoDB. The default name is
            /// <see cref="Builder.DefaultTableName"/>.
            /// </summary>
            ///
            /// <param name="tableName">The custom table name to use.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithTableName(string tableName);

            /// <summary>
            /// Builds the finalized <see cref="DynamoDbMetastoreImpl"/> with the parameters specified in the builder.
            /// </summary>
            ///
            /// <returns>The fully instantiated <see cref="DynamoDbMetastoreImpl"/> object.</returns>
            DynamoDbMetastoreImpl Build();
        }

        private interface IEndPointStep
        {
            /// <summary>
            /// Adds Endpoint config to the AWS DynamoDb client.
            /// </summary>
            ///
            /// <param name="endPoint">the service endpoint either with or without the protocol.</param>
            /// <param name="signingRegion">the region to use for SigV4 signing of requests (e.g. us-west-1).</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithEndPointConfiguration(string endPoint, string signingRegion);
        }

        private interface IRegionStep
        {
            /// <summary>
            /// Specifies the region for the AWS DynamoDb client. If this is not specified, then the region from
            /// <see cref="DynamoDbMetastoreImpl.NewBuilder"/> is used.
            /// </summary>
            ///
            /// <param name="region">The region for the DynamoDb client.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithRegion(string region);
        }

        internal string TableName { get; }

        internal IAmazonDynamoDB DbClient { get; }

        internal bool HasKeySuffix { get; }

        /// <summary>
        /// Builder method to initialize a <see cref="DynamoDbMetastoreImpl"/> instance.
        /// </summary>
        ///
        /// <param name="region">AWS region to use with the DynamoDB implementation.</param>
        /// <returns>The current <see cref="Builder"/> instance.</returns>
        public static Builder NewBuilder(string region)
        {
            return new Builder(region);
        }

        /// <inheritdoc />
        public Option<JObject> Load(string keyId, DateTimeOffset created)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadTimerOptions))
            {
                try
                {
                    GetItemOperationConfig config = new GetItemOperationConfig
                    {
                        AttributesToGet = new List<string> { AttributeKeyRecord },
                        ConsistentRead = true, // Always use strong consistency
                    };
                    Document result = table.GetItemAsync(
                        keyId,
                        created.ToUnixTimeSeconds(),
                        config).Result;
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

        /// <summary>
        /// Lookup the latest value associated with the keyId. The DynamoDB partition key is formed using the
        /// <see cref="DynamoDbMetastoreImpl.GetHashKey"/> method, which may or may not add a region suffix to it.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <returns>The latest <seealso cref="JObject"/> value associated with the keyId, if any.</returns>
        public Option<JObject> LoadLatest(string keyId)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(LoadLatestTimerOptions))
            {
                // Have to use query api to use limit and reverse sort order
                try
                {
                    QueryFilter filter =
                        new QueryFilter(PartitionKey, QueryOperator.Equal, GetHashKey(keyId));
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

        /// <summary>
        /// Stores the <see cref="JObject"/> value using the specified keyId and created time. The DynamoDB partition
        /// key is formed using the <see cref="DynamoDbMetastoreImpl.GetHashKey"/> method, which may or may not add a
        /// region suffix to it.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to store.</param>
        /// <param name="created">The created time to store.</param>
        /// <param name="value">The <see cref="JObject"/> value to store.</param>
        /// <returns><value>True</value> if the store succeeded, <value>False</value> if the store failed for a known
        /// condition like duplicate key.</returns>
        /// <exception cref="AppEncryptionException">Raise an exception in case of any other metastore errors.
        /// </exception>
        public bool Store(string keyId, DateTimeOffset created, JObject value)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(StoreTimerOptions))
            {
                try
                {
                    Document document = new Document
                    {
                        [PartitionKey] = GetHashKey(keyId),
                        [SortKey] = created.ToUnixTimeSeconds(),

                        // TODO Optimize JObject to Document conversion. Just need lambda that calls Document.
                        // Add and recurses for Dictionary and List types
                        [AttributeKeyRecord] = Document.FromJson(value.ToString()),
                    };

                    // Note conditional expression using attribute_not_exists has special semantics. Can be used on
                    // partition OR sort key alone to guarantee primary key uniqueness. It automatically checks for
                    // existence of this item's composite primary key and if it contains the specified attribute name,
                    // either of which is inherently required.
                    Expression expr = new Expression
                        { ExpressionStatement = "attribute_not_exists(" + PartitionKey + ")" };
                    PutItemOperationConfig config = new PutItemOperationConfig
                    {
                        ConditionalExpression = expr,
                    };

                    table.PutItemAsync(document, config).Wait();
                    return true;
                }
                catch (AggregateException ae)
                {
                    foreach (Exception exception in ae.InnerExceptions)
                    {
                        if (exception is ConditionalCheckFailedException)
                        {
                            Logger.LogInformation(
                                "Attempted to create duplicate key: {keyId} {created}",
                                keyId,
                                created);
                            return false;
                        }
                    }

                    Logger.LogError(ae, "Metastore error during store");
                    throw new AppEncryptionException("Metastore error:", ae);
                }
            }
        }

        /// <summary>
        /// Checks if the metastore has key suffixes enabled, and adds a region suffix to the <paramref name="key"/> if
        /// it does. This is done to enable Global Table Support. Adding a suffix to keys prevents multi-region writes
        /// from clobbering each other.
        /// </summary>
        ///
        /// <param name="key">The keyId part of the lookup key.</param>
        /// <returns>The region-suffixed key, if the metastore has that enabled, else returns the same input
        /// <paramref name="key"/>.</returns>
        private string GetHashKey(string key)
        {
            if (HasKeySuffix)
            {
                key = key + "_" + preferredRegion;
            }

            return key;
        }

        /// <summary>
        /// Builder class to create an instance of the <see cref="DynamoDbMetastoreImpl"/> class.
        /// </summary>
        public class Builder : IBuildStep, IEndPointStep, IRegionStep
        {
            #pragma warning disable SA1401
            internal readonly string PreferredRegion;
            internal IAmazonDynamoDB DbClient;
            internal bool HasKeySuffix;
            internal string TableName = DefaultTableName;
            #pragma warning restore SA1401

            private const string DefaultTableName = "EncryptionKey";
            private readonly AmazonDynamoDBConfig dbConfig = new AmazonDynamoDBConfig();
            private bool hasEndPoint;
            private bool hasRegion;

            /// <summary>
            /// Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            /// <param name="region">The region to use with AWS DynamoDB.</param>
            public Builder(string region)
            {
                PreferredRegion = region;
            }

            /// <inheritdoc/>
            public IBuildStep WithKeySuffix()
            {
                HasKeySuffix = true;
                return this;
            }

            /// <inheritdoc/>
            public IBuildStep WithTableName(string tableName)
            {
                TableName = tableName;
                return this;
            }

            /// <inheritdoc/>
            public IBuildStep WithEndPointConfiguration(string endPoint, string signingRegion)
            {
                if (!hasRegion)
                {
                    hasEndPoint = true;
                    dbConfig.ServiceURL = endPoint;
                    dbConfig.AuthenticationRegion = signingRegion;
                }

                return this;
            }

            /// <inheritdoc/>
            public IBuildStep WithRegion(string region)
            {
                if (!hasEndPoint)
                {
                    hasRegion = true;
                    dbConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
                }

                return this;
            }

            /// <summary>
            /// Builds the finalized <see cref="DynamoDbMetastoreImpl"/> object with the parameters specified in the
            /// <see cref="Builder"/>.
            /// </summary>
            ///
            /// <returns>The fully instantiated <see cref="DynamoDbMetastoreImpl"/> object.</returns>
            public DynamoDbMetastoreImpl Build()
            {
                if (!hasEndPoint && !hasRegion)
                {
                    dbConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(PreferredRegion);
                }

                DbClient = new AmazonDynamoDBClient(dbConfig);
                return new DynamoDbMetastoreImpl(this);
            }
        }
    }
}

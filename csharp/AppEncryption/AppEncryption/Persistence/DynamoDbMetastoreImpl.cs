using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        internal const string TableName = "EncryptionKey";
        internal const string PartitionKey = "Id";
        internal const string SortKey = "Created";
        internal const string AttributeKeyRecord = "KeyRecord";

        private static readonly TimerOptions LoadTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.load" };
        private static readonly TimerOptions LoadLatestTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.loadlatest" };
        private static readonly TimerOptions StoreTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".metastore.dynamodb.store" };

        private static readonly ILogger Logger = LogManager.CreateLogger<DynamoDbMetastoreImpl>();

        // Note this instance is thread-safe
        private readonly Table table;

        internal DynamoDbMetastoreImpl(IAmazonDynamoDB dbClient)
        {
            // Note this results in a network call. For now, cleaner than refactoring w/ thread-safe lazy loading
            table = Table.LoadTable(dbClient, TableName);
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

        public class Builder
        {
            public DynamoDbMetastoreImpl Build()
            {
                IAmazonDynamoDB dbClient = new AmazonDynamoDBClient();
                return new DynamoDbMetastoreImpl(dbClient);
            }
        }
    }
}

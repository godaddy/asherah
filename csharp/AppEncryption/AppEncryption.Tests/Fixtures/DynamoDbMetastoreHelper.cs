using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Fixtures;

[ExcludeFromCodeCoverage]
public static class DynamoDbMetastoreHelper
{
    public const string ExistingTestKey = "some_key";
    public static readonly Dictionary<string, object> ExistingKeyRecord = new()
    {
        {
            "ParentKeyMeta", new Dictionary<string, object>
            {
                { "KeyId", "_SK_api_ecomm" },
                { "Created", 1541461380 },
            }
        },
        { "Key", "fake-key-data" },
        { "Created", 1541461380 },
    };

    private const string PartitionKey = "Id";
    private const string SortKey = "Created";
    private const string AttributeKeyRecord = "KeyRecord";

    private static Table CreateTableInstance(IAmazonDynamoDB client, string tableName, string region)
    {
        // Create the old DynamoDB implementation
        var dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(region)
            .WithEndPointConfiguration(client.Config.ServiceURL, region)
            .WithTableName(tableName)
            .Build();

        // Create the table instance
        return new TableBuilder(client, dynamoDbMetastoreImpl.TableName)
            .AddHashKey(PartitionKey, DynamoDBEntryType.String)
            .AddRangeKey(SortKey, DynamoDBEntryType.Numeric)
            .Build();
    }

    private static async Task InsertDocumentAsync(Table table, string keyId, DateTimeOffset created, Dictionary<string, object> keyRecordDict)
    {
        var jObject = JObject.FromObject(keyRecordDict);
        var document = new Document
        {
            [PartitionKey] = keyId,
            [SortKey] = created.ToUnixTimeSeconds(),
            [AttributeKeyRecord] = Document.FromJson(jObject.ToString()),
        };

        await table.PutItemAsync(document);
    }

    public static async Task CreateTableSchema(AmazonDynamoDBClient client, string tableName)
    {
        var request = new CreateTableRequest
        {
            TableName = tableName,
            AttributeDefinitions =
            [
                new AttributeDefinition(PartitionKey, ScalarAttributeType.S),
                new AttributeDefinition(SortKey, ScalarAttributeType.N)
            ],
            KeySchema =
            [
                new KeySchemaElement(PartitionKey, KeyType.HASH),
                new KeySchemaElement(SortKey, KeyType.RANGE)
            ],
            ProvisionedThroughput = new ProvisionedThroughput(1L, 1L),
        };

        await client.CreateTableAsync(request);
    }

    public static async Task<DateTimeOffset> PrePopulateTestDataUsingOldMetastore(IAmazonDynamoDB client, string tableName, string region)
    {
        var table = CreateTableInstance(client, tableName, region);

        // Test data
        var testKeyWithRegionSuffix = ExistingTestKey + "_" + region;
        var created = DateTimeOffset.Now.AddDays(-1);

        // Pre-populate test data
        await InsertDocumentAsync(table, ExistingTestKey, created, ExistingKeyRecord);
        await InsertDocumentAsync(table, testKeyWithRegionSuffix, created, ExistingKeyRecord);

        return created;
    }

    public static async Task AddKeyRecordUsingOldMetastore(IAmazonDynamoDB client, string tableName, string region, string keyId, DateTimeOffset created, KeyRecord keyRecord)
    {
        var table = CreateTableInstance(client, tableName, region);

        // Convert KeyRecord to Dictionary<string, object> format
        var keyRecordDict = new Dictionary<string, object>
        {
            { "Key", keyRecord.Key },
            { "Created", keyRecord.Created.ToUnixTimeSeconds() }
        };

        // Add Revoked if it has a value
        if (keyRecord.Revoked.HasValue)
        {
            keyRecordDict["Revoked"] = keyRecord.Revoked.Value;
        }

        // Add ParentKeyMeta if it exists
        if (keyRecord.ParentKeyMeta != null)
        {
            keyRecordDict["ParentKeyMeta"] = new Dictionary<string, object>
            {
                { "KeyId", keyRecord.ParentKeyMeta.KeyId },
                { "Created", keyRecord.ParentKeyMeta.Created.ToUnixTimeSeconds() }
            };
        }

        // Insert the document
        await InsertDocumentAsync(table, keyId, created, keyRecordDict);
    }

    public static void VerifyKeyRecordUsingOldMetastore(IAmazonDynamoDB client, string tableName, string region, string keyId, KeyRecord expectedKeyRecord)
    {
        // Initialize metrics for the old implementation
        MetricsUtil.SetMetricsInstance(AppMetrics.CreateDefaultBuilder().Build());

        // Create the old DynamoDB implementation
        var dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.NewBuilder(region)
            .WithEndPointConfiguration(client.Config.ServiceURL, region)
            .WithTableName(tableName)
            .Build();

        // Load the key record using the old implementation
        var loadedJsonObject = dynamoDbMetastoreImpl.Load(keyId, expectedKeyRecord.Created);

        // Validate that the record was found
        Assert.True(loadedJsonObject.IsSome);
        var loadedKeyRecord = (JObject)loadedJsonObject;

        // Validate the properties
        Assert.Equal(expectedKeyRecord.Key, loadedKeyRecord["Key"]!.ToString());
        Assert.Equal(expectedKeyRecord.Created.ToUnixTimeSeconds(), loadedKeyRecord["Created"]!.ToObject<long>());

        // Validate Revoked
        if (expectedKeyRecord.Revoked.HasValue)
        {
            Assert.True(loadedKeyRecord.ContainsKey("Revoked"));
            Assert.Equal(expectedKeyRecord.Revoked.Value, loadedKeyRecord["Revoked"]!.ToObject<bool>());
        }
        else
        {
            Assert.False(loadedKeyRecord.ContainsKey("Revoked"));
        }

        // Validate ParentKeyMeta
        if (expectedKeyRecord.ParentKeyMeta != null)
        {
            Assert.True(loadedKeyRecord.ContainsKey("ParentKeyMeta"));
            var parentKeyMeta = loadedKeyRecord["ParentKeyMeta"]!.ToObject<JObject>();
            Assert.Equal(expectedKeyRecord.ParentKeyMeta.KeyId, parentKeyMeta["KeyId"]!.ToString());
            Assert.Equal(expectedKeyRecord.ParentKeyMeta.Created.ToUnixTimeSeconds(), parentKeyMeta["Created"]!.ToObject<long>());
        }
        else
        {
            Assert.False(loadedKeyRecord.ContainsKey("ParentKeyMeta"));
        }
    }
}

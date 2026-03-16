using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GoDaddy.Asherah.AppEncryption.Metastore;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore
{
    /// <summary>
    /// Provides an AWS DynamoDB based implementation of <see cref="IKeyMetastore"/> to store and retrieve system keys
    /// and intermediate keys as <see cref="KeyRecord"/> values.
    /// </summary>
    public class DynamoDbMetastore : IKeyMetastore
    {
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly DynamoDbMetastoreOptions _options;

        /// <summary>
        /// Provides an AWS DynamoDB based implementation of <see cref="IKeyMetastore"/> to store and retrieve system keys
        /// and intermediate keys as <see cref="KeyRecord"/> values.
        /// </summary>
        /// <param name="dynamoDbClient">The AWS DynamoDB client to use for operations.</param>
        /// <param name="options">Configuration options for the metastore.</param>
        /// <exception cref="ArgumentNullException">Thrown when dynamoDbClient or options is null.</exception>
        /// <exception cref="ArgumentException">Thrown when KeyRecordTableName is null or empty.</exception>
        public DynamoDbMetastore(IAmazonDynamoDB dynamoDbClient, DynamoDbMetastoreOptions options)
        {
            _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrEmpty(_options.KeyRecordTableName))
            {
                throw new ArgumentException("KeyRecordTableName must not be null or empty", nameof(options));
            }
        }

        private const string PartitionKey = "Id";
        private const string SortKey = "Created";
        private const string AttributeKeyRecord = "KeyRecord";

        /// <inheritdoc />
        public async Task<(bool found, IKeyRecord keyRecord)> TryLoadAsync(string keyId, DateTimeOffset created)
        {
            var request = new GetItemRequest
            {
                TableName = _options.KeyRecordTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    [PartitionKey] = new AttributeValue { S = keyId },
                    [SortKey] = new AttributeValue { N = created.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) }
                },
                ProjectionExpression = AttributeKeyRecord,
                ConsistentRead = true
            };

            var response = await _dynamoDbClient.GetItemAsync(request);

            if (response.Item != null && response.Item.TryGetValue(AttributeKeyRecord, out var keyRecordAttribute))
            {
                var keyRecord = ConvertAttributeValueToKeyRecord(keyRecordAttribute);
                return (true, keyRecord);
            }

            return (false, null);
        }

        /// <inheritdoc />
        public async Task<(bool found, IKeyRecord keyRecord)> TryLoadLatestAsync(string keyId)
        {
            var request = new QueryRequest
            {
                TableName = _options.KeyRecordTableName,
                KeyConditionExpression = $"{PartitionKey} = :keyId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":keyId"] = new AttributeValue { S = keyId }
                },
                ProjectionExpression = AttributeKeyRecord,
                ScanIndexForward = false, // Sort descending (latest first)
                Limit = 1, // Only get the latest item
                ConsistentRead = true
            };

            var response = await _dynamoDbClient.QueryAsync(request);

            if (response.Items == null || response.Items.Count == 0)
            {
                return (false, (IKeyRecord)null);
            }

            var item = response.Items[0];
            if (!item.TryGetValue(AttributeKeyRecord, out var keyRecordAttribute))
            {
                return (false, (IKeyRecord)null);
            }

            var keyRecord = ConvertAttributeValueToKeyRecord(keyRecordAttribute);
            return (true, keyRecord);
        }

        /// <inheritdoc />
        public async Task<bool> StoreAsync(string keyId, DateTimeOffset created, IKeyRecord keyRecord)
        {
            try
            {
                var keyRecordMap = new Dictionary<string, AttributeValue>
                {
                    ["Key"] = new AttributeValue { S = keyRecord.Key },
                    ["Created"] = new AttributeValue { N = keyRecord.Created.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) }
                };

                // Only add Revoked if it has a value
                if (keyRecord.Revoked.HasValue)
                {
                    keyRecordMap["Revoked"] = new AttributeValue { BOOL = keyRecord.Revoked.Value };
                }

                // Only add ParentKeyMeta if it exists
                if (keyRecord.ParentKeyMeta != null)
                {
                    keyRecordMap["ParentKeyMeta"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["KeyId"] = new AttributeValue { S = keyRecord.ParentKeyMeta.KeyId },
                            ["Created"] = new AttributeValue { N = keyRecord.ParentKeyMeta.Created.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) }
                        }
                    };
                }

                var keyRecordAttribute = new AttributeValue { M = keyRecordMap };

                var item = new Dictionary<string, AttributeValue>
                {
                    [PartitionKey] = new AttributeValue { S = keyId },
                    [SortKey] = new AttributeValue { N = created.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },
                    [AttributeKeyRecord] = keyRecordAttribute
                };

                var request = new PutItemRequest
                {
                    TableName = _options.KeyRecordTableName,
                    Item = item,
                    ConditionExpression = $"attribute_not_exists({PartitionKey})"
                };

                await _dynamoDbClient.PutItemAsync(request);
                return true;
            }
            catch (ConditionalCheckFailedException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public string GetKeySuffix()
        {
            if (_options.KeySuffix != null)
            {
                return _options.KeySuffix;
            }

            return _dynamoDbClient.Config.RegionEndpoint?.SystemName;
        }

        /// <summary>
        /// Creates a new <see cref="IDynamoDbMetastoreBuilder"/> instance for constructing a <see cref="DynamoDbMetastore"/>.
        /// </summary>
        /// <returns>A new <see cref="IDynamoDbMetastoreBuilder"/> instance.</returns>
        public static IDynamoDbMetastoreBuilder NewBuilder() => new DynamoDbMetastoreBuilder();

        private static KeyRecord ConvertAttributeValueToKeyRecord(AttributeValue keyRecordAttribute)
        {
            if (keyRecordAttribute.M == null)
            {
                throw new ArgumentException("KeyRecord attribute must be a Map", nameof(keyRecordAttribute));
            }

            var map = keyRecordAttribute.M;

            if (!map.TryGetValue("Key", out var keyAttr) || keyAttr.S == null)
            {
                throw new ArgumentException("KeyRecord must contain Key field", nameof(keyRecordAttribute));
            }
            var keyString = keyAttr.S;

            // Extract Created (Unix timestamp)
            if (!map.TryGetValue("Created", out var createdAttr) || createdAttr.N == null)
            {
                throw new ArgumentException("KeyRecord must contain Created field", nameof(keyRecordAttribute));
            }
            var created = DateTimeOffset.FromUnixTimeSeconds(long.Parse(createdAttr.N, CultureInfo.InvariantCulture));

            // Extract Revoked (optional boolean)
            bool? revoked = null;
            if (map.TryGetValue("Revoked", out var revokedAttr) && revokedAttr.BOOL.HasValue)
            {
                revoked = revokedAttr.BOOL.Value;
            }

            // Extract ParentKeyMeta (optional map)
            KeyMeta parentKeyMeta = null;
            if (map.TryGetValue("ParentKeyMeta", out var parentMetaAttr) && parentMetaAttr.M != null)
            {
                var parentMetaMap = parentMetaAttr.M;
                if (parentMetaMap.TryGetValue("KeyId", out var parentKeyIdAttr) && parentMetaMap.TryGetValue("Created", out var parentCreatedAttr))
                {
                    var parentKeyId = parentKeyIdAttr.S;
                    var parentCreated = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parentCreatedAttr.N, CultureInfo.InvariantCulture));
                    parentKeyMeta = new KeyMeta { KeyId = parentKeyId, Created = parentCreated };
                }
            }

            return new KeyRecord(created, keyString, revoked, parentKeyMeta);
        }
    }
}

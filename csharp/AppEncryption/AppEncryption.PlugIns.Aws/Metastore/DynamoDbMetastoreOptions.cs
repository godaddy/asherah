namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore
{
    /// <summary>
    /// Configuration options for DynamoDbMetastore.
    /// </summary>
    public class DynamoDbMetastoreOptions
    {
        /// <summary>
        /// The table name for the KeyRecord storage
        /// </summary>
        public string KeyRecordTableName { get; set; } = "KeyRecord";

        /// <summary>
        /// The key suffix to use for key IDs to support DynamoDB Global Tables. When null (default),
        /// the region from the DynamoDB client is used as the suffix. Set to an empty string to disable
        /// key suffix for backwards compatibility with existing data that was stored without suffixes.
        /// Can also be set to an arbitrary string for custom suffix values.
        /// </summary>
        public string KeySuffix { get; set; }
    };
}

using Amazon.DynamoDBv2;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore
{
    /// <summary>
    /// Builder interface for constructing a <see cref="DynamoDbMetastore"/> instance.
    /// </summary>
    public interface IDynamoDbMetastoreBuilder
    {
        /// <summary>
        /// Sets the DynamoDB client to use for operations.
        /// </summary>
        /// <param name="dynamoDbClient">The AWS DynamoDB client.</param>
        /// <returns>The current <see cref="IDynamoDbMetastoreBuilder"/> instance.</returns>
        IDynamoDbMetastoreBuilder WithDynamoDbClient(IAmazonDynamoDB dynamoDbClient);

        /// <summary>
        /// Sets the configuration options for the metastore.
        /// </summary>
        /// <param name="options">The configuration options.</param>
        /// <returns>The current <see cref="IDynamoDbMetastoreBuilder"/> instance.</returns>
        IDynamoDbMetastoreBuilder WithOptions(DynamoDbMetastoreOptions options);

        /// <summary>
        /// Builds the <see cref="DynamoDbMetastore"/> instance.
        /// </summary>
        /// <returns>A new <see cref="DynamoDbMetastore"/> instance.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the DynamoDB client is not set.</exception>
        DynamoDbMetastore Build();
    }
}

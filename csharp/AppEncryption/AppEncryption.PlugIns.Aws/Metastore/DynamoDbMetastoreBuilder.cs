using System;
using Amazon.DynamoDBv2;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore
{
    internal sealed class DynamoDbMetastoreBuilder : IDynamoDbMetastoreBuilder
    {
        private IAmazonDynamoDB _dynamoDbClient;
        private DynamoDbMetastoreOptions _options;

        public IDynamoDbMetastoreBuilder WithDynamoDbClient(IAmazonDynamoDB dynamoDbClient)
        {
            _dynamoDbClient = dynamoDbClient;
            return this;
        }

        public IDynamoDbMetastoreBuilder WithOptions(DynamoDbMetastoreOptions options)
        {
            _options = options;
            return this;
        }

        public DynamoDbMetastore Build()
        {
            if (_dynamoDbClient == null)
            {
                throw new InvalidOperationException("DynamoDB client must be set using WithDynamoDbClient()");
            }

            return new DynamoDbMetastore(_dynamoDbClient, _options ?? new DynamoDbMetastoreOptions());
        }
    }
}

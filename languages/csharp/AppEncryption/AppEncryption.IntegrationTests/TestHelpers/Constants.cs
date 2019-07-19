namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers
{
    public static class Constants
    {
        /// <summary>
        /// KMS
        /// </summary>
        public const string KeyManagementStaticMasterKey = "secretmasterkey!";
        public const string KeyManagementAws = "aws";

        /// <summary>
        /// Metastore
        /// </summary>
        public const string MetastoreAdo = "ado";
        public const string MetastoreDynamoDb = "dynamodb";

        /// <summary>
        /// IDs & Keys
        /// </summary>
        public const string DefaultSystemId = "system";
        public const string DefaultProductId = "product";
        public const string DefaultPartitionId = "partition";
        public const int KeyExpiryDays = 30;

        /// <summary>
        /// Multithreaded Test Parameters
        /// </summary>
        public const int NumThreads = 100;
        public const int PayloadSizeBytes = 100;
        public const int NumIterations = 100;
        public const int NumRequests = 100;

        /// <summary>
        /// Regression Test Configuration
        /// </summary>
        public const string ConfigFile = "CONFIG_FILE";
        public const string DefaultConfigFile = "config.yaml";
        public const string AdoConnectionString = "METASTORE_ADO_CONNECTIONSTRING";
        public const string KmsAwsRegionTuples = "KMS_AWS_REGION_TUPLES";
        public const string KmsAwsPreferredRegion = "KMS_AWS_PREFERRED_REGION";
        public const string MetaStoreType = "metaStoreType";
        public const string KmsType = "kmsType";
    }
}

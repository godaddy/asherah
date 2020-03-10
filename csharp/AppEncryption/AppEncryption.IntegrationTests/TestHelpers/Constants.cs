namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers
{
    public static class Constants
    {
        /// <summary>
        /// KMS
        /// </summary>
        public const string KeyManagementStaticMasterKey = "secretmasterkey!";
        public const string KeyManagementAws = "aws";
        public const string KeyManagementStatic = "static";
        public const string DefaultKeyManagementType = KeyManagementStatic;

        /// <summary>
        /// Metastore
        /// </summary>
        public const string MetastoreAdo = "ado";
        public const string MetastoreDynamoDb = "dynamodb";
        public const string MetastoreMemory = "memory";
        public const string DefaultMetastoreType = MetastoreMemory;

        /// <summary>
        /// IDs & Keys
        /// </summary>
        public const string DefaultServiceId = "service";
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
        public const string MetastoreAdoConnectionString = "metastoreAdoConnectionString";
        public const string KmsAwsRegionTuples = "kmsAwsRegionArnTuples";
        public const string KmsAwsPreferredRegion = "kmsAwsPreferredRegion";
        public const string MetastoreType = "metastoreType";
        public const string KmsType = "kmsType";
        public const string DefaultPreferredRegion = "us-west-2";
    }
}

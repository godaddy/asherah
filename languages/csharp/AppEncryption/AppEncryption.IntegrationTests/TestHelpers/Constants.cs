namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers
{
    public static class Constants
    {
        public const string KeyManagementStaticMasterKey = "secretmasterkey!";

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
    }
}

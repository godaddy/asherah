using System;

namespace GoDaddy.Asherah.Cltf
{
    public static class Constants
    {
        public const string KeyManagementStaticMasterKey = "mysupersecretstaticmasterkey!!!!";

        public const string DefaultServiceId = "service";
        public const string DefaultProductId = "product";
        public const string DefaultPartitionId = "partition";

        public const string FileDirectory = "encrypted_files";
        public const string FileName = "csharp_encrypted";

        public const int KeyExpiryDays = 30;
        public const int RevokeCheckMinutes = 60;

        internal static readonly string AdoConnectionString;

        private static readonly string AdoDatabaseName;
        private static readonly string AdoUsername;
        private static readonly string AdoPassword;

        static Constants()
        {
            AdoDatabaseName = Environment.GetEnvironmentVariable("TEST_DB_NAME");
            AdoUsername = Environment.GetEnvironmentVariable("TEST_DB_USER");
            AdoPassword = Environment.GetEnvironmentVariable("TEST_DB_PASSWORD");
            AdoConnectionString = "server=localhost;" +
                                  "uid=" + AdoUsername +
                                  ";pwd=" + AdoPassword +
                                  ";sslmode=none;" +
                                  "Initial Catalog=" + AdoDatabaseName;
        }
    }
}

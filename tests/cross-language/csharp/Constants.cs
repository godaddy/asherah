namespace GoDaddy.Asherah.Cltf
{
    public static class Constants
    {
        public const string KeyManagementStaticMasterKey = "mysupersecretstaticmasterkey!!!!";

        public const string AdoConnectionString =
            "server=127.0.0.1;uid=root;pwd=Password123;sslmode=none;Initial Catalog=test";

        public const string DefaultServiceId = "service";
        public const string DefaultProductId = "product";
        public const string DefaultPartitionId = "partition";

        public const string FileDirectory = "encrypted_files";
        public const string FileName = "csharp_encrypted";

        public const int KeyExpiryDays = 30;
        public const int RevokeCheckMinutes = 60;
    }
}

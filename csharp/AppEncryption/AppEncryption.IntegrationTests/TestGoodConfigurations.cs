using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests
{
    internal class TestGoodConfigurations : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { TestBaseConfig() };
            yield return new object[] { TestDefaultCryptoPolicyConfig() };
            yield return new object[] { TestBlankCipherConfig() };
            yield return new object[] { TestSomeOptionalsConfig() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IConfiguration TestBaseConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "aws" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "aes-256-gcm" },
                    { "keyRotationStrategy", "queued" },
                }).Build();
        }

        private IConfiguration TestDefaultCryptoPolicyConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "static" },
                    { "kmsStaticKey", "staticKey" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", string.Empty },
                    { "cipher", string.Empty },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBlankCipherConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "static" },
                    { "kmsStaticKey", "staticKey" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", string.Empty },
                    { "canCacheSystemKeys", "false" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestSomeOptionalsConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "static" },
                    { "kmsStaticKey", "staticKey" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", string.Empty },
                    { "keyRotationStrategy", "inline" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "sessionCacheExpireMillis", "30000" },
                }).Build();
        }
    }
}

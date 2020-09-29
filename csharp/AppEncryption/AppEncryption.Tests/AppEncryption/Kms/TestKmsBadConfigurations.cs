using System;
using System.Collections.Generic;
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    internal class TestKmsBadConfigurations : TheoryData<IConfiguration, Type>
    {
        public TestKmsBadConfigurations()
        {
            Type exType = typeof(Exception);

            Add(null, typeof(ArgumentNullException));
            Add(TestBadKeyExpirationDays(), typeof(FormatException));
            Add(TestKmsAwsNoPreferredRegion(), exType);
            Add(TestBadRevokeCheckMinutesConfig(), typeof(FormatException));
            Add(TestEmptyConfig(), exType);
            Add(TestBadCryptoEngineConfig(), exType);
            Add(TestBadCipherConfig(), typeof(CipherNotSupportedException));
            Add(TestBadKeyRotationStrategyConfig(), exType);
            Add(TestMissingRevokeCheckMinutesConfig(), exType);
            Add(TestMissingKeyExpirationDays(), exType);
            Add(TestBadCanCacheSystemKeysConfig(), typeof(FormatException));
            Add(TestBadCanCacheIntermediateKeysConfig(), typeof(FormatException));
            Add(TestBadKmsTypeConfig(), exType);
            Add(TestNoStaticKmsKey(), exType);
            Add(TestNoKmsType(), exType);
        }

        private IConfiguration TestEmptyConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>()).Build();
        }

        private IConfiguration TestBadKeyExpirationDays()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "asdf" },
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

        private IConfiguration TestKmsAwsNoPreferredRegion()
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

        private IConfiguration TestMissingKeyExpirationDays()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
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

        private IConfiguration TestBadRevokeCheckMinutesConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "asdf" },
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

        private IConfiguration TestMissingRevokeCheckMinutesConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
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

        private IConfiguration TestBadCryptoEngineConfig()
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
                    { "cryptoEngine", "NotVeryBouncy" },
                    { "cipher", string.Empty },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadCipherConfig()
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
                    { "cipher", "rot13" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadKeyRotationStrategyConfig()
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
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "spin-it!" },
                }).Build();
        }

        private IConfiguration TestBadCanCacheSystemKeysConfig()
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
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "maybe" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadCanCacheIntermediateKeysConfig()
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
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "maybe" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadKmsTypeConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "uncertain" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestNoStaticKmsKey()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsType", "static" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestNoKmsType()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "memory" },
                    { "metastoreAdoConnectionString", string.Empty },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }
    }
}

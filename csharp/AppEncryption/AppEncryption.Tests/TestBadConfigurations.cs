using System;
using System.Collections.Generic;
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    internal class TestBadConfigurations : TheoryData<IConfiguration, Type>
    {
        public TestBadConfigurations()
        {
            Type exType = typeof(Exception);

            Add(null, typeof(ArgumentNullException));
            Add(TestBadKeyExpirationDays(), typeof(FormatException));
            Add(TestBadRevokeCheckMinutesConfig(), typeof(FormatException));
            Add(TestBadMetastoreTypeConfig(), typeof(AppEncryptionException));
            Add(TestEmptyConfig(), exType);
            Add(TestBadCryptoEngineConfig(), exType);
            Add(TestBadCipherConfig(), typeof(CipherNotSupportedException));
            Add(TestBadKeyRotationStrategyConfig(), exType);
            Add(TestMissingRevokeCheckMinutesConfig(), exType);
            Add(TestMissingKeyExpirationDays(), exType);
            Add(TestBadCanCacheSystemKeysConfig(), typeof(FormatException));
            Add(TestBadCanCacheIntermediateKeysConfig(), typeof(FormatException));
            Add(TestBadKmsTypeConfig(), exType);
            Add(TestMissingAdoConnectionStringConfig(), typeof(AppEncryptionException));
            Add(TestBadAdoFactoryTypeConfig(), typeof(ArgumentException));
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

        private IConfiguration TestBadMetastoreTypeConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "kangaroo-pouch" },
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

        private IConfiguration TestMissingAdoConnectionStringConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "ado" },
                    { "kmsType", "static" },
                    { "kmsStaticKey", "staticKey" },
                    { "kmsAwsPreferredRegion", string.Empty },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "aes-256-gcm" },
                    { "canCacheSystemKeys", "true" },
                    { "canCacheIntermediateKeys", "true" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadAdoFactoryTypeConfig()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "metastoreType", "ado" },
                    { "metastoreAdoConnectionString", "host=blah" },
                    { "metastoreAdoFactoryType", "crazy-sql-db" },
                    { "kmsType", "static" },
                    { "kmsStaticKey", "staticKey" },
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

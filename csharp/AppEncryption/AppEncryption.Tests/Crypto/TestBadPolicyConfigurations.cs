using System;
using System.Collections.Generic;
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto
{
    internal class TestBadPolicyConfigurations : TheoryData<IConfiguration, Type>
    {
        public TestBadPolicyConfigurations()
        {
            Type exType = typeof(Exception);

            Add(null, typeof(ArgumentNullException));
            Add(TestEmptyConfig(), exType);
            Add(TestBadKeyExpirationDays(), typeof(FormatException));
            Add(TestBadRevokeCheckMinutes(), typeof(FormatException));
            Add(TestBadKeyRotationStrategy(), exType);
            Add(TestBadCipher(), typeof(CipherNotSupportedException));
            Add(TestBadCryptoEngine(), exType);
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
                    { "keyRotationStrategy", "queued" },
                }).Build();
        }

        private IConfiguration TestBadRevokeCheckMinutes()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "asdf" },
                    { "keyRotationStrategy", "queued" },
                }).Build();
        }

        private IConfiguration TestBadKeyRotationStrategy()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "keyRotationStrategy", "skip-it!" },
                }).Build();
        }

        private IConfiguration TestBadCipher()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "cryptoEngine", "Bouncy" },
                    { "cipher", "rot13" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }

        private IConfiguration TestBadCryptoEngine()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "keyExpirationDays", "90" },
                    { "revokeCheckMinutes", "30" },
                    { "cryptoEngine", "enigma-machine" },
                    { "keyRotationStrategy", "inline" },
                }).Build();
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class CryptoKeyHolder
    {
        private static readonly AeadEnvelopeCrypto Crypto = new BouncyAes256GcmCrypto();

        private CryptoKeyHolder(CryptoKey systemKey, CryptoKey intermediateKey)
        {
            SystemKey = systemKey;
            IntermediateKey = intermediateKey;
        }

        internal CryptoKey SystemKey { get; }

        internal CryptoKey IntermediateKey { get; }

        internal static CryptoKeyHolder GenerateIKSK()
        {
            // Subtracting 1/2 the KEY_EXPIRY_DAYS so that during the course of testing any new keys
            // can be created with the current timestamp (this avoids conflicts when the test needs
            // to create a new key based on the current timestamp.)
            DateTimeOffset created = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromSeconds(1))
                .Subtract(TimeSpan.FromDays(Constants.KeyExpiryDays / 2.0));
            CryptoKey systemKey = Crypto.GenerateKey(created);
            CryptoKey intermediateKey = Crypto.GenerateKey(created);

            return new CryptoKeyHolder(systemKey, intermediateKey);
        }
    }
}

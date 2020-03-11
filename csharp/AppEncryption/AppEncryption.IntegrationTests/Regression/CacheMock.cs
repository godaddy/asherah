using System;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class CacheMock
    {
        private static readonly AeadEnvelopeCrypto Crypto = new BouncyAes256GcmCrypto();

        private CacheMock(SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache, SecureCryptoKeyDictionary<DateTimeOffset> intermediateKeyCache)
        {
            SystemKeyCache = systemKeyCache;
            IntermediateKeyCache = intermediateKeyCache;
        }

        internal SecureCryptoKeyDictionary<DateTimeOffset> IntermediateKeyCache { get; }

        internal SecureCryptoKeyDictionary<DateTimeOffset> SystemKeyCache { get; }

        internal static CacheMock CreateCacheMock(
            KeyState cacheIK,
            KeyState cacheSK,
            CryptoKeyHolder cryptoKeyHolder)
        {
            Mock<SecureCryptoKeyDictionary<DateTimeOffset>> systemKeyCacheSpy =
                new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(long.MaxValue / 2) { CallBase = true };
            Mock<SecureCryptoKeyDictionary<DateTimeOffset>> intermediateKeyCacheSpy =
                new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(long.MaxValue / 2) { CallBase = true };

            if (cacheSK != KeyState.Empty)
            {
                CryptoKey systemKey = cryptoKeyHolder.SystemKey;
                if (cacheSK == KeyState.Retired)
                {
                    // We create a revoked copy of the same key
                    DateTimeOffset created = systemKey.GetCreated();
                    systemKey = systemKey
                        .WithKey(bytes => Crypto.GenerateKeyFromBytes(bytes, created, true));
                }

                systemKeyCacheSpy.Object.PutAndGetUsable(systemKey.GetCreated(), systemKey);
            }

            if (cacheIK != KeyState.Empty)
            {
                CryptoKey intermediateKey = cryptoKeyHolder.IntermediateKey;
                if (cacheIK == KeyState.Retired)
                {
                    // We create a revoked copy of the same key
                    DateTimeOffset created = intermediateKey.GetCreated();
                    intermediateKey = intermediateKey
                        .WithKey(bytes => Crypto.GenerateKeyFromBytes(bytes, created, true));
                }

                intermediateKeyCacheSpy.Object.PutAndGetUsable(intermediateKey.GetCreated(), intermediateKey);
            }

            return new CacheMock(systemKeyCacheSpy.Object, intermediateKeyCacheSpy.Object);
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy
{
    public class DummyKeyManagementService : KeyManagementService
    {
        private readonly CryptoKey encryptionKey;
        private readonly AeadEnvelopeCrypto crypto;

        public DummyKeyManagementService(IConfiguration configuration)
        {
            var cryptoPolicy = BasicExpiringCryptoPolicy.BuildWithConfiguration(configuration);
            crypto = cryptoPolicy.GetCrypto();
            encryptionKey = crypto.GenerateKey();
        }

        public override byte[] EncryptKey(CryptoKey key)
        {
            return crypto.EncryptKey(key, encryptionKey);
        }

        public override CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return crypto.DecryptKey(keyCipherText, keyCreated, encryptionKey, revoked);
        }

        public override string ToString()
        {
            return typeof(DummyKeyManagementService).FullName + "[kms_arn=LOCAL, crypto=" + crypto + "]";
        }

        public override void Dispose()
        {
            encryptionKey.Dispose();
        }
    }
}

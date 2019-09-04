using System;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy
{
    public class DummyKeyManagementService : KeyManagementService
    {
        private readonly CryptoKey encryptionKey;
        private readonly BouncyAes256GcmCrypto crypto = new BouncyAes256GcmCrypto();

        public DummyKeyManagementService()
        {
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
    }
}

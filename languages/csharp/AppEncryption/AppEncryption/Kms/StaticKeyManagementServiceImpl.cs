using System;
using System.Text;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    public class StaticKeyManagementServiceImpl : KeyManagementService
    {
        private readonly CryptoKey encryptionKey;
        private readonly BouncyAes256GcmCrypto crypto = new BouncyAes256GcmCrypto();

        public StaticKeyManagementServiceImpl(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            Secret secretKey = new TransientSecretFactory().CreateSecret(keyBytes);

            encryptionKey = new SecretCryptoKey(secretKey, DateTimeOffset.UtcNow, false);
        }

        public override byte[] EncryptKey(CryptoKey key)
        {
            return crypto.EncryptKey(key, encryptionKey);
        }

        public override CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return crypto.DecryptKey(keyCipherText, keyCreated, encryptionKey, revoked);
        }
    }
}

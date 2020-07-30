using System;
using System.Text;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    /// <summary>
    /// An implementation of <see cref="KeyManagementService"/> that uses <see cref="BouncyAes256GcmCrypto"/> to
    /// encrypt/decrypt keys.
    /// Note: This should never be used in a production environment.
    /// </summary>
    public class StaticKeyManagementServiceImpl : KeyManagementService
    {
        private readonly CryptoKey encryptionKey;
        private readonly BouncyAes256GcmCrypto crypto = new BouncyAes256GcmCrypto();

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticKeyManagementServiceImpl"/> class. It uses a hard coded
        /// static master key,
        /// </summary>
        ///
        /// <param name="key">The static master key to use.</param>
        public StaticKeyManagementServiceImpl(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            Secret secretKey = new TransientSecretFactory().CreateSecret(keyBytes);

            encryptionKey = new SecretCryptoKey(secretKey, DateTimeOffset.UtcNow, false);
        }

        /// <inheritdoc/>
        public override byte[] EncryptKey(CryptoKey key)
        {
            return crypto.EncryptKey(key, encryptionKey);
        }

        /// <inheritdoc/>
        public override CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return crypto.DecryptKey(keyCipherText, keyCreated, encryptionKey, revoked);
        }
    }
}

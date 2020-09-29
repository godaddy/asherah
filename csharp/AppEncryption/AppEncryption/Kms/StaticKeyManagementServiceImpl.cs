using System;
using System.Text;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    /// <summary>
    /// An implementation of <see cref="KeyManagementService"/> that uses <see cref="AeadEnvelopeCrypto"/> to
    /// encrypt/decrypt keys.
    /// Note: This should never be used in a production environment.
    /// </summary>
    public class StaticKeyManagementServiceImpl : KeyManagementService
    {
        private readonly CryptoKey encryptionKey;
        private readonly AeadEnvelopeCrypto crypto;
        private readonly Secret secretKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticKeyManagementServiceImpl"/> class. It uses a hard coded
        /// static master key.
        /// </summary>
        ///
        /// <param name="key">The static master key to use.</param>
        public StaticKeyManagementServiceImpl(string key, CryptoPolicy policy, IConfiguration configuration)
        {
            crypto = policy.GetCrypto();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            secretKey = new ProtectedMemorySecretFactory(configuration).CreateSecret(keyBytes);

            encryptionKey = new SecretCryptoKey(secretKey, DateTimeOffset.UtcNow, false);
        }

        public override void Dispose()
        {
            encryptionKey.Dispose();
            secretKey.Dispose();
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

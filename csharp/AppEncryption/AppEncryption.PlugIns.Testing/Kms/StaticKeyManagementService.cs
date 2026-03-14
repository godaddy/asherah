using System;
using System.Text;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Kms
{
    /// <summary>
    /// An implementation of <see cref="IKeyManagementService"/> that uses a static master key and
    /// <see cref="BouncyAes256GcmCrypto"/> to encrypt/decrypt keys.
    /// NOTE: This should NEVER be used in a production environment.
    /// </summary>
    public sealed class StaticKeyManagementService : IKeyManagementService, IDisposable
    {
        private readonly SecretCryptoKey _encryptionKey;
        private readonly BouncyAes256GcmCrypto _crypto = new BouncyAes256GcmCrypto();

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticKeyManagementService"/> class with a
        /// randomly generated master key (GUID). Useful for tests that need an ephemeral KMS and
        /// do not care about key stability.
        /// </summary>
        public StaticKeyManagementService()
            : this(Guid.NewGuid().ToString("N"))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticKeyManagementService"/> class.
        /// </summary>
        /// <param name="key">The static master key to use.</param>
        public StaticKeyManagementService(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            Secret secretKey = new TransientSecretFactory().CreateSecret(keyBytes);
            _encryptionKey = new SecretCryptoKey(secretKey, DateTimeOffset.UtcNow, false);
        }

        /// <inheritdoc />
        public byte[] EncryptKey(CryptoKey key)
        {
            return _crypto.EncryptKey(key, _encryptionKey);
        }

        /// <inheritdoc />
        public CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return _crypto.DecryptKey(keyCipherText, keyCreated, _encryptionKey, revoked);
        }

        /// <inheritdoc />
        public Task<byte[]> EncryptKeyAsync(CryptoKey key)
        {
            return Task.FromResult(EncryptKey(key));
        }

        /// <inheritdoc />
        public Task<CryptoKey> DecryptKeyAsync(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return Task.FromResult(DecryptKey(keyCipherText, keyCreated, revoked));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _encryptionKey?.Dispose();
            _crypto?.Dispose();
        }
    }
}

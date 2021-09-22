using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.Crypto.BufferUtils;
using GoDaddy.Asherah.Crypto.Keys;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.Crypto.Envelope
{
    public abstract class AeadEnvelopeCrypto : AeadCrypto
    {
        /// <summary>
        /// Encrypts a <see cref="CryptoKey"/> with another <see cref="CryptoKey"/>.
        /// </summary>
        ///
        /// <param name="key">The key to encrypt.</param>
        /// <param name="keyEncryptionKey">The key encryption key.</param>
        /// <returns>The encrypted key bytes.</returns>
        public virtual byte[] EncryptKey(CryptoKey key, CryptoKey keyEncryptionKey)
        {
            return key.WithKey(keyBytes => Encrypt(keyBytes, keyEncryptionKey));
        }

        /// <summary>
        /// Decrypts an encrypted key.
        /// </summary>
        ///
        /// <param name="encryptedKey">The encrypted key bytes.</param>
        /// <param name="encryptedKeyCreated">The creation time of the encrypted key.</param>
        /// <param name="keyEncryptionKey">The key encryption key.</param>
        /// <returns>A decrypted <see cref="CryptoKey"/> object.</returns>
        public virtual byte[] DecryptKey(byte[] encryptedKey, DateTimeOffset encryptedKeyCreated, CryptoKey keyEncryptionKey)
        {
            byte[] decryptedKeyBytes = DecryptKey(encryptedKey, encryptedKeyCreated, keyEncryptionKey, false);

            return decryptedKeyBytes;
        }

        /// <summary>
        /// Decrypts an encrypted key.
        /// </summary>
        ///
        /// <param name="encryptedKey">The encrypted key bytes.</param>
        /// <param name="encryptedKeyCreated">The creation time of the encrypted key.</param>
        /// <param name="keyEncryptionKey">The key encryption key.</param>
        /// <param name="revoked">The revocation status of the key.</param>
        /// <returns>A decrypted <see cref="CryptoKey"/> object.</returns>
        public virtual byte[] DecryptKey(
            byte[] encryptedKey, DateTimeOffset encryptedKeyCreated, CryptoKey keyEncryptionKey, bool revoked)
        {
            byte[] decryptedKey = Decrypt(encryptedKey, keyEncryptionKey);
            try
            {
                return decryptedKey;
            }
            finally
            {
                ManagedBufferUtils.WipeByteArray(decryptedKey);
            }
        }

        /// <summary>
        /// Encrypts the payload and the key to create the data row record.
        /// </summary>
        ///
        /// <param name="plainText">The payload to be encrypted.</param>
        /// <param name="keyEncryptionKey">The key encryption key.</param>
        /// <param name="userState">The KeyMeta for the <see cref="keyEncryptionKey"/>.</param>
        /// <returns>A <see cref="EnvelopeEncryptResult"/>object/data row record (DRR).</returns>
        public virtual EnvelopeEncryptResult EnvelopeEncrypt(CryptoKey dataEncryptionKey, byte[] plainText, CryptoKey keyEncryptionKey, object userState)
        {
                EnvelopeEncryptResult result = new EnvelopeEncryptResult
                {
                    CipherText = Encrypt(plainText, dataEncryptionKey),
                    EncryptedKey = EncryptKey(dataEncryptionKey, keyEncryptionKey),
                    UserState = userState,
                };

                return result;
        }

        /// <summary>
        /// Decrypts the encryptedKey and then uses the decrypted key to decrypt the encrypted payload in the data row record.
        /// </summary>
        ///
        /// <param name="cipherText">The encrypted payload.</param>
        /// <param name="encryptedKey">The encrypted key.</param>
        /// <param name="keyCreated">The creation time of the data row record.</param>
        /// <param name="keyEncryptionKey">The key encryption key.</param>
        /// <returns>The decrypted payload.</returns>
        public virtual byte[] EnvelopeDecrypt(byte[] cipherText, byte[] encryptedKey, DateTimeOffset keyCreated, CryptoKey keyEncryptionKey)
        {
            byte[] x = DecryptKey(encryptedKey, keyCreated, keyEncryptionKey);
            return x;

            // using (CryptoKey key = GenerateKeyFromBytes(x, keyCreated, false))
            // {
            //     // ReSharper disable once AccessToDisposedClosure
            //     return Decrypt(cipherText, key);
            // }
        }
    }
}

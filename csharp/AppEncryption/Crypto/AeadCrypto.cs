using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using GoDaddy.Asherah.Crypto.BufferUtils;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.Crypto
{
    public abstract class AeadCrypto
    {
        private const int BitsPerByte = 8;

        private static readonly RandomNumberGenerator CryptoRandom = RandomNumberGenerator.Create();

        private readonly NonceGenerator nonceGenerator;
        private readonly ISecretFactory secretFactory;

        protected AeadCrypto()
        {
            secretFactory = new TransientSecretFactory();
            nonceGenerator = new NonceGenerator();
        }

        public abstract byte[] Encrypt(byte[] input, CryptoKey key);

        public abstract byte[] Decrypt(byte[] input, CryptoKey key);

        public virtual CryptoKey GenerateKey()
        {
            return GenerateRandomCryptoKey();
        }

        public virtual CryptoKey GenerateKey(DateTimeOffset created)
        {
            return GenerateRandomCryptoKey(created);
        }

        /// <summary>
        /// Generates a CryptoKey using the provided source bytes. NOTE: you MUST wipe out the source bytes after
        /// the completion of this call!
        /// </summary>
        /// <param name="sourceBytes">Bytes used to generate the key</param>
        /// <returns>A CryptoKey generated using the sourceBytes</returns>
        public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes)
        {
            return GenerateKeyFromBytes(sourceBytes, DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Generates a CryptoKey using the provided source bytes and created time. NOTE: you MUST wipe out the
        /// source bytes after the completion of this call!
        /// </summary>
        /// <param name="sourceBytes">Bytes used to generate the key</param>
        /// <param name="created">Time of creation of key</param>
        /// <returns>A CryptoKey generated using the sourceBytes</returns>
        public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes, DateTimeOffset created)
        {
            return GenerateKeyFromBytes(sourceBytes, created, false);
        }

        /// <summary>
        /// Generates a CryptoKey using the provided source bytes, created time, and revoked flag. NOTE: you MUST
        /// wipe out the source bytes after the completion of this call!
        /// </summary>
        /// <param name="sourceBytes">Bytes used to generate the key</param>
        /// <param name="created">Time of creation of key</param>
        /// <param name="revoked">Specifies if the key is revoked or not</param>
        /// <returns>A CryptoKey generated using the sourceBytes</returns>
        public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes, DateTimeOffset created, bool revoked)
        {
            byte[] clonedBytes = sourceBytes.Clone() as byte[];
            Secret newKeySecret = GetSecretFactory().CreateSecret(clonedBytes);

            return new SecretCryptoKey(newKeySecret, created, revoked);
        }

        /// <summary>
        /// Generates a random CryptoKey using the current time as the created time.
        /// </summary>
        /// <returns>a generated random CryptoKey</returns>
        protected internal virtual CryptoKey GenerateRandomCryptoKey()
        {
            return GenerateRandomCryptoKey(DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Generates a random <code>CryptoKey</code> using the given time as the created time.
        /// </summary>
        /// <param name="created">created the time to associate the generated <code>CryptoKey</code> with</param>
        /// <returns>a generated random <code>CryptoKey</code></returns>
        /// <exception cref="ArgumentException">Throws an exception if key length is invalid</exception>
        protected internal virtual CryptoKey GenerateRandomCryptoKey(DateTimeOffset created)
        {
            int keyLengthBits = GetKeySizeBits();
            if (keyLengthBits % BitsPerByte != 0)
            {
                throw new ArgumentException("Invalid key length: " + keyLengthBits);
            }

            byte[] keyBytes = new byte[keyLengthBits / BitsPerByte];
            CryptoRandom.GetBytes(keyBytes);
            try
            {
                return GenerateKeyFromBytes(keyBytes, created);
            }
            finally
            {
                ManagedBufferUtils.WipeByteArray(keyBytes);
            }
        }

        protected internal abstract int GetKeySizeBits();

        protected internal virtual ISecretFactory GetSecretFactory()
        {
            return secretFactory;
        }

        protected abstract int GetNonceSizeBits();

        protected abstract int GetMacSizeBits();

        protected byte[] GetAppendedNonce(byte[] cipherTextAndNonce)
        {
            int nonceByteSize = GetNonceSizeBits() / BitsPerByte;
            byte[] nonce = new byte[nonceByteSize];
            Array.Copy(cipherTextAndNonce, cipherTextAndNonce.Length - nonceByteSize, nonce, 0, nonceByteSize);
            return nonce;
        }

        protected void AppendNonce(byte[] cipherText, byte[] nonce)
        {
            Array.Copy(nonce, 0, cipherText, cipherText.Length - nonce.Length, nonce.Length);
        }

        protected byte[] GenerateNonce()
        {
            return nonceGenerator.CreateNonce(GetNonceSizeBits());
        }
    }
}

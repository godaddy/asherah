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
  public abstract class AeadCrypto : IDisposable
  {
    private const int BitsPerByte = 8;

    private static readonly RandomNumberGenerator CryptoRandom = RandomNumberGenerator.Create();

    private readonly NonceGenerator nonceGenerator;
    private readonly TransientSecretFactory secretFactory;

    protected AeadCrypto()
    {
      secretFactory = new TransientSecretFactory();
      nonceGenerator = new NonceGenerator();
    }

    /// <summary>
    /// Encrypts the provided payload.
    /// </summary>
    ///
    /// <param name="input">Payload bytes to be encrypted.</param>
    /// <param name="key">The <see cref="CryptoKey"/> to encrypt the payload with.</param>
    /// <returns>An encrypted payload.</returns>
    public abstract byte[] Encrypt(byte[] input, CryptoKey key);

    /// <summary>
    /// Decrypts an encrypted payload.
    /// </summary>
    ///
    /// <param name="input">The encrypted payload.</param>
    /// <param name="key">The <see cref="CryptoKey"/> to decrypt the payload with.</param>
    /// <returns>An decrypted payload.</returns>
    public abstract byte[] Decrypt(byte[] input, CryptoKey key);

    /// <summary>
    /// Generates a new <see cref="CryptoKey"/>.
    /// </summary>
    ///
    /// <returns>A newly generated <see cref="CryptoKey"/>.</returns>
    public virtual CryptoKey GenerateKey()
    {
      return GenerateRandomCryptoKey();
    }

    /// <summary>
    /// Generates a new <see cref="CryptoKey"/>.
    /// </summary>
    ///
    /// <param name="created">The timestamp to be used for key creation.</param>
    /// <returns>A newly generated <see cref="CryptoKey"/>.</returns>
    public virtual CryptoKey GenerateKey(DateTimeOffset created)
    {
      return GenerateRandomCryptoKey(created);
    }

    /// <summary>
    /// Generates a CryptoKey using the provided source bytes.
    /// NOTE: you MUST wipe out the source bytes after the completion of this call!.
    /// </summary>
    ///
    /// <param name="sourceBytes">Bytes used to generate the key.</param>
    /// <returns>A <see cref="CryptoKey"/> generated using the sourceBytes.</returns>
    public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes)
    {
      return GenerateKeyFromBytes(sourceBytes, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Generates a <see cref="CryptoKey"/> using the provided source bytes and created time.
    /// NOTE: you MUST wipe out the source bytes after the completion of this call!.
    /// </summary>
    ///
    /// <param name="sourceBytes">Bytes used to generate the key.</param>
    /// <param name="created">Time of creation of key.</param>
    /// <returns>A <see cref="CryptoKey"/> generated using the sourceBytes.</returns>
    public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes, DateTimeOffset created)
    {
      return GenerateKeyFromBytes(sourceBytes, created, false);
    }

    /// <summary>
    /// Generates a <see cref="CryptoKey"/> using the provided source bytes, created time, and revoked flag.
    /// NOTE: you MUST wipe out the source bytes after the completion of this call!.
    /// </summary>
    ///
    /// <param name="sourceBytes">Bytes used to generate the key.</param>
    /// <param name="created">Time of creation of key.</param>
    /// <param name="revoked">Specifies if the key is revoked or not.</param>
    /// <returns>A <see cref="CryptoKey"/> generated using the sourceBytes.</returns>
    public virtual CryptoKey GenerateKeyFromBytes(byte[] sourceBytes, DateTimeOffset created, bool revoked)
    {
      byte[] clonedBytes = sourceBytes.Clone() as byte[];
      Secret newKeySecret = GetSecretFactory().CreateSecret(clonedBytes);

      return new SecretCryptoKey(newKeySecret, created, revoked);
    }

    /// <summary>
    /// Generates a random <see cref="CryptoKey"/> using the current time as the created time.
    /// </summary>
    ///
    /// <returns>A generated random <see cref="CryptoKey"/>.</returns>
    protected internal virtual CryptoKey GenerateRandomCryptoKey()
    {
      return GenerateRandomCryptoKey(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Generates a random <see cref="CryptoKey"/> using the given time as the created time.
    /// </summary>
    ///
    /// <param name="created"> The time to associate the generated <see cref="CryptoKey"/> with.</param>
    /// <returns>A generated random <see cref="CryptoKey"/>.</returns>
    /// <exception cref="ArgumentException">Throws an exception if key length is invalid.</exception>
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

    protected static void AppendNonce(byte[] cipherText, byte[] nonce)
    {
      Array.Copy(nonce, 0, cipherText, cipherText.Length - nonce.Length, nonce.Length);
    }

    protected byte[] GenerateNonce()
    {
      return nonceGenerator.CreateNonce(GetNonceSizeBits());
    }

    public void Dispose()
    {
      secretFactory?.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}

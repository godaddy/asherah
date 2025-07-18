using System;
using System.Security.Cryptography;

namespace GoDaddy.Asherah.Crypto
{
  public class NonceGenerator
  {
    private const int BitsPerByte = 8;
    private readonly RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();

    /// <summary>
    /// Generates a nonce.
    /// </summary>
    ///
    /// <param name="bits">Number of bits to use for nonce generation.</param>
    /// <returns>An array of nonce bytes.</returns>
    /// <exception cref="ArgumentException">If bits is not a multiple of 8.</exception>
    public byte[] CreateNonce(int bits)
    {
      if (bits % BitsPerByte != 0)
      {
        throw new ArgumentException($"Bits parameter must be a multiple of {BitsPerByte}");
      }

      byte[] keyBytes = new byte[bits / BitsPerByte];
      randomNumberGenerator.GetBytes(keyBytes);

      return keyBytes;
    }
  }
}

using System;
using System.Security.Cryptography;

namespace GoDaddy.Asherah.Crypto
{
    public class NonceGenerator
    {
        private const int BitsPerByte = 8;
        private readonly RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();

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

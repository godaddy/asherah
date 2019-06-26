using GoDaddy.Asherah.Crypto.Keys;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace GoDaddy.Asherah.Crypto.Engine.BouncyCastle
{
    public class BouncyAes256GcmCrypto : BouncyAeadCrypto
    {
        private const int NonceSizeBits = 96;
        private const int KeySizeBits = 256;
        private const int MacSizeBits = 128;

        protected internal override int GetKeySizeBits()
        {
            return KeySizeBits;
        }

        protected override IAeadBlockCipher GetNewAeadBlockCipherInstance()
        {
            return new GcmBlockCipher(new AesEngine());
        }

        protected override AeadParameters GetParameters(CryptoKey key, byte[] nonce)
        {
            return key.WithKey(keyBytes => new AeadParameters(new KeyParameter(keyBytes), MacSizeBits, nonce));
        }

        protected override int GetNonceSizeBits()
        {
            return NonceSizeBits;
        }

        protected override int GetMacSizeBits()
        {
            return MacSizeBits;
        }
    }
}

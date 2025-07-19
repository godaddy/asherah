using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Engine.BouncyCastle
{
    public class BouncyAes256GcmCryptoTest : GenericAeadCryptoTest
    {
        protected override AeadEnvelopeCrypto GetCryptoInstance()
        {
            return new BouncyAes256GcmCrypto();
        }
    }
}

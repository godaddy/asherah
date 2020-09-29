using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Engine.BouncyCastle
{
    public class BouncyAes256GcmCryptoTest : GenericAeadCryptoTest
    {
        public BouncyAes256GcmCryptoTest(ConfigFixture configFixture)
            : base(configFixture)
        {
        }

        protected override AeadEnvelopeCrypto GetCryptoInstance(IConfiguration configuration)
        {
            return new BouncyAes256GcmCrypto(configuration);
        }
    }
}

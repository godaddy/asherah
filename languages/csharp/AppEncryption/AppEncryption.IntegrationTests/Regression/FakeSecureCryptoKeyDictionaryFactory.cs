using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class FakeSecureCryptoKeyDictionaryFactory<T> : SecureCryptoKeyDictionaryFactory<T>
    {
        private readonly SecureCryptoKeyDictionary<T> secureCryptoKeyDictionary;

        public FakeSecureCryptoKeyDictionaryFactory(SecureCryptoKeyDictionary<T> secureCryptoKeyDictionary)
            : base(null)
        {
            this.secureCryptoKeyDictionary = secureCryptoKeyDictionary;
        }

        public override SecureCryptoKeyDictionary<T> CreateSecureCryptoKeyDictionary()
        {
            return secureCryptoKeyDictionary;
        }
    }
}

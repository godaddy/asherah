using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.Crypto.Keys
{
    public class SecureCryptoKeyDictionaryFactory<TResult>
    {
        private readonly CryptoPolicy cryptoPolicy;

        public SecureCryptoKeyDictionaryFactory(CryptoPolicy cryptoPolicy)
        {
            this.cryptoPolicy = cryptoPolicy;
        }

        public virtual SecureCryptoKeyDictionary<TResult> CreateSecureCryptoKeyDictionary()
        {
            return new SecureCryptoKeyDictionary<TResult>(cryptoPolicy.GetRevokeCheckPeriodMillis());
        }
    }
}

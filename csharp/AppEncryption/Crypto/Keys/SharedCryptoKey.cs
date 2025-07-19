using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.Crypto.Keys
{
    public class SharedCryptoKey : CryptoKey
    {
        internal SharedCryptoKey(CryptoKey sharedKey)
        {
            SharedKey = sharedKey;
        }

        internal CryptoKey SharedKey { get; }

        /// <inheritdoc />
        public override DateTimeOffset GetCreated()
        {
            return SharedKey.GetCreated();
        }

        /// <inheritdoc />
        public override void WithKey(Action<byte[]> actionWithKey)
        {
            SharedKey.WithKey(actionWithKey);
        }

        /// <inheritdoc />
        public override TResult WithKey<TResult>(Func<byte[], TResult> actionWithKey)
        {
            return SharedKey.WithKey(actionWithKey);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // SharedCryptoKey doesn't *own* any secrets so it doesn't have anything to dispose
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override bool IsRevoked()
        {
            return SharedKey.IsRevoked();
        }

        /// <inheritdoc />
        public override void MarkRevoked()
        {
            SharedKey.MarkRevoked();
        }
    }
}

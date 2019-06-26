using System;

namespace GoDaddy.Asherah.Crypto.Keys
{
    public abstract class CryptoKey : IDisposable
    {
        public abstract DateTimeOffset GetCreated();

        public abstract void WithKey(Action<byte[]> actionWithKey);

        public abstract TResult WithKey<TResult>(Func<byte[], TResult> actionWithKey);

        public abstract void Dispose();

        public abstract bool IsRevoked();

        public abstract void MarkRevoked();
    }
}

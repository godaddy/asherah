using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.SecureMemory;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]

namespace GoDaddy.Asherah.Crypto.Keys
{
    public class SecretCryptoKey : CryptoKey
    {
        private readonly DateTimeOffset created;

        private volatile bool revoked;

        public SecretCryptoKey(CryptoKey otherKey)
        {
            Secret = ((SecretCryptoKey)otherKey).Secret.CopySecret();
            created = ((SecretCryptoKey)otherKey).created;
            revoked = ((SecretCryptoKey)otherKey).revoked;
        }

        public SecretCryptoKey(Secret secret, DateTimeOffset created, bool revoked)
        {
            Secret = secret;
            this.created = created;
            this.revoked = revoked;
        }

        internal virtual Secret Secret { get; }

        public override DateTimeOffset GetCreated()
        {
            return created;
        }

        public override void WithKey(Action<byte[]> actionWithKey)
        {
            Secret.WithSecretBytes(actionWithKey);
        }

        public override TResult WithKey<TResult>(Func<byte[], TResult> actionWithKey)
        {
            return Secret.WithSecretBytes(actionWithKey);
        }

        public override void Dispose()
        {
            Secret.Dispose();
        }

        public override bool IsRevoked()
        {
            return revoked;
        }

        public override void MarkRevoked()
        {
            revoked = true;
        }
    }
}

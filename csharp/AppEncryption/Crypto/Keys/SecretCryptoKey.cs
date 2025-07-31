using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.SecureMemory;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "This class does not have a finalizer and does not need to suppress finalization.")]

namespace GoDaddy.Asherah.Crypto.Keys
{
    public class SecretCryptoKey : CryptoKey
    {
        private readonly DateTimeOffset created;

        private volatile bool revoked;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecretCryptoKey"/> class by copying the contents from a
        /// <see cref="CryptoKey"/>.
        /// </summary>
        ///
        /// <param name="otherKey">The <see cref="CryptoKey"/> to copy the contents from.</param>
        public SecretCryptoKey(CryptoKey otherKey)
        {
            Secret = ((SecretCryptoKey)otherKey).Secret.CopySecret();
            created = ((SecretCryptoKey)otherKey).created;
            revoked = ((SecretCryptoKey)otherKey).revoked;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecretCryptoKey"/> class.
        /// </summary>
        ///
        /// <param name="secret">A <see cref="Secret"/> object.</param>
        /// <param name="created">The creation time of the key.</param>
        /// <param name="revoked">Indicates if the key is revoked.</param>
        public SecretCryptoKey(Secret secret, DateTimeOffset created, bool revoked)
        {
            Secret = secret;
            this.created = created;
            this.revoked = revoked;
        }

        internal virtual Secret Secret { get; }

        /// <inheritdoc />
        public override DateTimeOffset GetCreated()
        {
            return created;
        }

        /// <inheritdoc />
        public override void WithKey(Action<byte[]> actionWithKey)
        {
            Secret.WithSecretBytes(actionWithKey);
        }

        /// <inheritdoc />
        public override TResult WithKey<TResult>(Func<byte[], TResult> actionWithKey)
        {
            return Secret.WithSecretBytes(actionWithKey);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            Secret.Dispose();
        }

        /// <inheritdoc />
        public override bool IsRevoked()
        {
            return revoked;
        }

        /// <inheritdoc />
        public override void MarkRevoked()
        {
            revoked = true;
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Holds the cryptographic context used for a session's envelope encryption operations.
    /// </summary>
    internal class SessionCryptoContext : IEnvelopeCryptoContext
    {
        /// <inheritdoc/>
        public AeadEnvelopeCrypto Crypto { get; }

        /// <inheritdoc/>
        public CryptoPolicy Policy { get; }

        /// <inheritdoc/>
        public SecureCryptoKeyDictionary<DateTimeOffset> SystemKeyCache { get; }

        /// <inheritdoc/>
        public SecureCryptoKeyDictionary<DateTimeOffset> IntermediateKeyCache { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionCryptoContext"/> class.
        /// </summary>
        /// <param name="crypto">The envelope crypto implementation.</param>
        /// <param name="policy">The crypto policy.</param>
        /// <param name="systemKeyCache">The shared system key cache.</param>
        public SessionCryptoContext(
            AeadEnvelopeCrypto crypto,
            CryptoPolicy policy,
            SecureCryptoKeyDictionary<DateTimeOffset> systemKeyCache)
        {
            Crypto = crypto;
            Policy = policy;
            SystemKeyCache = systemKeyCache;
            IntermediateKeyCache = new SecureCryptoKeyDictionary<DateTimeOffset>(policy.GetRevokeCheckPeriodMillis());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            IntermediateKeyCache?.Dispose();
        }
    }
}

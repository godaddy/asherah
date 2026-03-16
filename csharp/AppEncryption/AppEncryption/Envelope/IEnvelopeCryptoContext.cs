using System;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// Defines the cryptographic context used for envelope encryption operations.
    /// </summary>
    public interface IEnvelopeCryptoContext : IDisposable
    {
        /// <summary>
        /// Gets the envelope crypto implementation for encryption/decryption operations.
        /// </summary>
        AeadEnvelopeCrypto Crypto { get; }

        /// <summary>
        /// Gets the crypto policy that dictates key expiration, caching, and rotation behaviors.
        /// </summary>
        CryptoPolicy Policy { get; }

        /// <summary>
        /// Gets the shared cache for system keys.
        /// </summary>
        SecureCryptoKeyDictionary<DateTimeOffset> SystemKeyCache { get; }

        /// <summary>
        /// Gets the cache for intermediate keys.
        /// </summary>
        SecureCryptoKeyDictionary<DateTimeOffset> IntermediateKeyCache { get; }
    }
}

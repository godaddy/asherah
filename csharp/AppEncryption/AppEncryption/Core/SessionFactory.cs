using System;
using System.Collections.Concurrent;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// A session factory is required to generate cryptographic sessions.
    /// This is the modernized implementation that supports only byte[] sessions.
    /// </summary>
    public class SessionFactory : IDisposable
    {
        private readonly string _productId;
        private readonly string _serviceId;
        private readonly IKeyMetastore _keyMetastore;
        private readonly CryptoPolicy _cryptoPolicy;
        private readonly IKeyManagementService _keyManagementService;
        private readonly ILogger _logger;
        private readonly BouncyAes256GcmCrypto _crypto;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> _systemKeyCache;
        private readonly bool _canCacheSessions;
        private readonly ConcurrentDictionary<string, CachedEncryptionSession> _sessionCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionFactory"/> class.
        /// </summary>
        /// <param name="productId">A unique identifier for a product.</param>
        /// <param name="serviceId">A unique identifier for a service.</param>
        /// <param name="keyMetastore">The key metastore for storing keys.</param>
        /// <param name="cryptoPolicy">The crypto policy that dictates behaviors.</param>
        /// <param name="keyManagementService">The key management service for master key operations.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        internal SessionFactory(
            string productId,
            string serviceId,
            IKeyMetastore keyMetastore,
            CryptoPolicy cryptoPolicy,
            IKeyManagementService keyManagementService,
            ILogger logger)
        {
            _productId = productId;
            _serviceId = serviceId;
            _keyMetastore = keyMetastore;
            _cryptoPolicy = cryptoPolicy;
            _keyManagementService = keyManagementService;
            _logger = logger;
            _crypto = new BouncyAes256GcmCrypto();
            _systemKeyCache = new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis());
            _canCacheSessions = cryptoPolicy.CanCacheSessions();
            _sessionCache = new ConcurrentDictionary<string, CachedEncryptionSession>();
        }

        /// <summary>
        /// Creates a new builder for constructing a <see cref="SessionFactory"/>.
        /// </summary>
        /// <param name="productId">A unique identifier for a product.</param>
        /// <param name="serviceId">A unique identifier for a service.</param>
        /// <returns>A new <see cref="ISessionFactoryBuilder"/> instance.</returns>
        public static ISessionFactoryBuilder NewBuilder(string productId, string serviceId)
        {
            return new SessionFactoryBuilder(productId, serviceId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                _systemKeyCache?.Dispose();
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Unexpected exception during system key cache dispose");
            }

            foreach (var session in _sessionCache.Values)
            {
                try
                {
                    session.DisposeUnderlying();
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Unexpected exception during session dispose");
                }
            }

            _sessionCache.Clear();
        }

        /// <summary>
        /// Gets an encryption session for the specified partition.
        /// </summary>
        /// <param name="partitionId">A unique identifier for a session.</param>
        /// <returns>An <see cref="IEncryptionSession"/> for the specified partition.</returns>
        public IEncryptionSession GetSession(string partitionId)
        {
            if (_canCacheSessions)
            {
                return _sessionCache.GetOrAdd(partitionId, CreateCachedSession);
            }

            return CreateNewSession(partitionId);
        }

        private CachedEncryptionSession CreateCachedSession(string partitionId)
        {
            return new CachedEncryptionSession(CreateNewSession(partitionId));
        }

        private EncryptionSession CreateNewSession(string partitionId)
        {
            var suffix = _keyMetastore.GetKeySuffix();
            var partition = new SessionPartition(partitionId, _serviceId, _productId, suffix);
            var cryptoContext = new SessionCryptoContext(_crypto, _cryptoPolicy, _systemKeyCache);

            var envelopeEncryption = new EnvelopeEncryption(
                partition,
                _keyMetastore,
                _keyManagementService,
                cryptoContext,
                _logger);

            return new EncryptionSession(envelopeEncryption);
        }
    }
}

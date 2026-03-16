using System;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Builder for creating <see cref="SessionFactory"/> instances.
    /// </summary>
    internal class SessionFactoryBuilder : ISessionFactoryBuilder
    {
        private readonly string _productId;
        private readonly string _serviceId;

        private IKeyMetastore _keyMetastore;
        private CryptoPolicy _cryptoPolicy;
        private IKeyManagementService _keyManagementService;
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionFactoryBuilder"/> class.
        /// </summary>
        /// <param name="productId">A unique identifier for a product.</param>
        /// <param name="serviceId">A unique identifier for a service.</param>
        /// <exception cref="ArgumentException">Thrown when productId or serviceId is null or empty.</exception>
        public SessionFactoryBuilder(string productId, string serviceId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(productId));
            }

            if (string.IsNullOrEmpty(serviceId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serviceId));
            }

            _productId = productId;
            _serviceId = serviceId;
        }

        /// <inheritdoc/>
        public ISessionFactoryBuilder WithKeyMetastore(IKeyMetastore keyMetastore)
        {
            _keyMetastore = keyMetastore;
            return this;
        }

        /// <inheritdoc/>
        public ISessionFactoryBuilder WithCryptoPolicy(CryptoPolicy cryptoPolicy)
        {
            _cryptoPolicy = cryptoPolicy;
            return this;
        }

        /// <inheritdoc/>
        public ISessionFactoryBuilder WithKeyManagementService(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
            return this;
        }

        /// <inheritdoc/>
        public ISessionFactoryBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        /// <inheritdoc/>
        public SessionFactory Build()
        {
            if (_keyMetastore == null)
            {
                throw new InvalidOperationException("Key metastore must be set using WithKeyMetastore()");
            }

            if (_cryptoPolicy == null)
            {
                throw new InvalidOperationException("Crypto policy must be set using WithCryptoPolicy()");
            }

            if (_keyManagementService == null)
            {
                throw new InvalidOperationException("Key management service must be set using WithKeyManagementService()");
            }

            if (_logger == null)
            {
                throw new InvalidOperationException("Logger must be set using WithLogger()");
            }

            return new SessionFactory(
                _productId,
                _serviceId,
                _keyMetastore,
                _cryptoPolicy,
                _keyManagementService,
                _logger);
        }
    }
}

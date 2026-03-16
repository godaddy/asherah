using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Builder interface for creating <see cref="SessionFactory"/> instances.
    /// </summary>
    public interface ISessionFactoryBuilder
    {
        /// <summary>
        /// Sets the key metastore for the session factory.
        /// </summary>
        /// <param name="keyMetastore">The <see cref="IKeyMetastore"/> implementation to use for storing keys.</param>
        /// <returns>This builder instance for method chaining.</returns>
        ISessionFactoryBuilder WithKeyMetastore(IKeyMetastore keyMetastore);

        /// <summary>
        /// Sets the crypto policy for the session factory.
        /// </summary>
        /// <param name="cryptoPolicy">The <see cref="CryptoPolicy"/> implementation that dictates
        /// the various behaviors of Asherah.</param>
        /// <returns>This builder instance for method chaining.</returns>
        ISessionFactoryBuilder WithCryptoPolicy(CryptoPolicy cryptoPolicy);

        /// <summary>
        /// Sets the key management service for the session factory.
        /// </summary>
        /// <param name="keyManagementService">The <see cref="IKeyManagementService"/> implementation that generates
        /// the top level master key and encrypts the system keys using the master key.</param>
        /// <returns>This builder instance for method chaining.</returns>
        ISessionFactoryBuilder WithKeyManagementService(IKeyManagementService keyManagementService);

        /// <summary>
        /// Sets the logger for the session factory.
        /// </summary>
        /// <param name="logger">The logger implementation to use.</param>
        /// <returns>This builder instance for method chaining.</returns>
        ISessionFactoryBuilder WithLogger(ILogger logger);

        /// <summary>
        /// Builds the finalized session factory with the configured parameters.
        /// </summary>
        /// <returns>The fully instantiated <see cref="SessionFactory"/>.</returns>
        SessionFactory Build();
    }
}

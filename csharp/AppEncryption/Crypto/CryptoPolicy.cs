using System;
using GoDaddy.Asherah.Crypto.ExtensionMethods;

namespace GoDaddy.Asherah.Crypto
{
    /// <summary>
    /// The crypto policy interface dictates the various behaviors of Asherah.
    /// </summary>
    public abstract class CryptoPolicy
    {
        public enum KeyRotationStrategy
        {
            Inline,
            Queued,
        }

        /// <summary>
        /// Checks if the key is expired.
        /// </summary>
        ///
        /// <param name="keyCreationDate">The key creation date as a <see cref="DateTimeOffset"/> object.</param>
        /// <returns><value>true</value> if the key is expired, else <value>false</value>.</returns>
        public abstract bool IsKeyExpired(DateTimeOffset keyCreationDate);

        /// <summary>
        /// Gets the key revoke check time in milliseconds.
        /// </summary>
        ///
        /// <returns>The key revoke check time in milliseconds.</returns>
        public abstract long GetRevokeCheckPeriodMillis();

        /// <summary>
        /// Checks if the <see cref="CryptoPolicy" /> allows caching of SystemKeys.
        /// </summary>
        ///
        /// <returns><value>true</value> if SystemKey caching is enabled, else <value>false</value>.</returns>
        public abstract bool CanCacheSystemKeys();

        /// <summary>
        /// Checks if the <see cref="CryptoPolicy" /> allows caching of IntermediateKeys.
        /// </summary>
        ///
        /// <returns><value>true</value> if IntermediateKey caching is enabled, else <value>false</value>.</returns>
        public abstract bool CanCacheIntermediateKeys();

        /// <summary>
        /// Checks if the <see cref="CryptoPolicy" /> allows caching of Sessions.
        /// </summary>
        ///
        /// <returns><value>true</value> if Session caching is enabled, else <value>false</value>.</returns>
        public abstract bool CanCacheSessions();

        /// <summary>
        /// Gets the maximum number of session objects that can be cached.
        /// </summary>
        ///
        /// <returns>The size of the session cache.</returns>
        public abstract long GetSessionCacheMaxSize();

        /// <summary>
        /// Gets the session cache expiry time limit in milliseconds.
        /// </summary>
        ///
        /// <returns>The session cache expiry time limit in milliseconds.</returns>
        public abstract long GetSessionCacheExpireMillis();

        /// <summary>
        /// Checks if a notification should be sent when a DRK is using an expired IK.
        /// </summary>
        ///
        /// <returns><code>true</code> if notification sending is enabled, else <code>false</code>.</returns>
        public abstract bool NotifyExpiredIntermediateKeyOnRead();

        /// <summary>
        /// Checks if a notification should be sent when an expired SK is used during read.
        /// </summary>
        ///
        /// <returns><code>true</code> if notification sending is enabled, else <code>false</code>.</returns>
        public abstract bool NotifyExpiredSystemKeyOnRead();

        /// <summary>
        /// Get the key rotation strategy.
        /// </summary>
        ///
        /// <returns>A <see cref="CryptoPolicy.KeyRotationStrategy"/> object.</returns>
        public abstract KeyRotationStrategy GetKeyRotationStrategy();

        /// <summary>
        /// Checks if the key rotation strategy is <see cref="KeyRotationStrategy.Inline"/>.
        /// </summary>
        ///
        /// <returns><code>true</code> if key rotation is <see cref="KeyRotationStrategy.Inline"/>, else <code>false</code>.</returns>
        public virtual bool IsInlineKeyRotation()
        {
            return GetKeyRotationStrategy() == KeyRotationStrategy.Inline;
        }

        /// <summary>
        /// Checks if the key rotation strategy is <see cref="KeyRotationStrategy.Queued"/>.
        /// </summary>
        ///
        /// <returns><code>true</code> if key rotation is <see cref="KeyRotationStrategy.Queued"/>, else <code>false</code>.</returns>
        public virtual bool IsQueuedKeyRotation()
        {
            return GetKeyRotationStrategy() == KeyRotationStrategy.Queued;
        }

        /// <summary>
        /// Truncate the SystemKey created time to the nearest minute.
        /// </summary>
        ///
        /// <param name="dateTimeOffset">A <see cref="DateTimeOffset"/> object.</param>
        /// <returns>A <see cref="DateTimeOffset"/> object truncated to the nearest minute. </returns>
        public virtual DateTimeOffset TruncateToSystemKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Truncate the IntermediateKey created time to the nearest minute.
        /// </summary>
        ///
        /// <param name="dateTimeOffset">A <see cref="DateTimeOffset"/> object.</param>
        /// <returns>A <see cref="DateTimeOffset"/> object truncated to the nearest minute. </returns>
        public virtual DateTimeOffset TruncateToIntermediateKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromMinutes(1));
        }
    }
}

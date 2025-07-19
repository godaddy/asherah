using System;

namespace GoDaddy.Asherah.Crypto
{
    /// <summary>
    /// A Crypto Policy that allows easy customization of the expiration duration and caching TTL,
    /// with default values for key strategy, caching, and notification options:
    /// <para>
    ///   - Key Rotation Strategy: Inline<br/>
    ///   - Caching of System and Intermediate Keys is allowed<br/>
    ///   - Session Caching is disabled<br/>
    ///   - Notifications of reads using expired keys is disabled<br/>
    /// </para>
    /// All of the default values can be modified using the optional builder methods.
    /// </summary>
    public class BasicExpiringCryptoPolicy : CryptoPolicy
    {
        private readonly long keyExpirationMillis;
        private readonly long revokeCheckMillis;

        // NOTE: Defaults for these are taken from the Builder class, below
        private readonly KeyRotationStrategy keyRotationStrategy;
        private readonly bool canCacheSystemKeys;
        private readonly bool canCacheIntermediateKeys;
        private readonly bool canCacheSessions;
        private readonly long sessionCacheMaxSize;
        private readonly long sessionCacheExpireMillis;
        private readonly bool notifyExpiredSystemKeyOnRead;
        private readonly bool notifyExpiredIntermediateKeyOnRead;

        private BasicExpiringCryptoPolicy(Builder builder)
        {
            keyExpirationMillis = (long)TimeSpan.FromDays(builder.KeyExpirationDays).TotalMilliseconds;
            revokeCheckMillis = (long)TimeSpan.FromMinutes(builder.RevokeCheckMinutes).TotalMilliseconds;
            keyRotationStrategy = builder.KeyRotationStrategy;
            canCacheSystemKeys = builder.CanCacheSystemKeys;
            canCacheIntermediateKeys = builder.CanCacheIntermediateKeys;
            canCacheSessions = builder.CanCacheSessions;
            sessionCacheMaxSize = builder.SessionCacheMaxSize;
            sessionCacheExpireMillis = builder.SessionCacheExpireMillis;
            notifyExpiredSystemKeyOnRead = builder.NotifyExpiredSystemKeyOnRead;
            notifyExpiredIntermediateKeyOnRead = builder.NotifyExpiredIntermediateKeyOnRead;
        }

        public interface IKeyExpirationDaysStep
        {
            /// <summary>
            /// Specifies the number of days after which the keys expire.
            /// </summary>
            ///
            /// <param name="days">The expiration limit of keys.</param>
            /// <returns>The current <see cref="IRevokeCheckMinutesStep"/> instance.</returns>
            IRevokeCheckMinutesStep WithKeyExpirationDays(int days);
        }

        public interface IRevokeCheckMinutesStep
        {
            /// <summary>
            /// Specifies the revoke check limit (in minutes) for keys.
            /// </summary>
            ///
            /// <param name="minutes">The revoke check limit (in minutes) for keys.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithRevokeCheckMinutes(int minutes);
        }

        public interface IBuildStep
        {
            /// <summary>
            /// Specifies the key rotation strategy to use. Defaults to <value>DefaultKeyRotationStrategy</value>.
            /// </summary>
            /// <param name="rotationStrategy">the strategy to use.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithRotationStrategy(KeyRotationStrategy rotationStrategy);

            /// <summary>
            /// Specifies whether to cache system keys. Defaults to <value>DefaultCanCacheSystemKeys</value>.
            /// </summary>
            /// <param name="cacheSystemKeys">whether to cache system keys.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithCanCacheSystemKeys(bool cacheSystemKeys);

            /// <summary>
            /// Specifies whether to cache intermediate keys. Defaults to <value>DefaultCanCacheIntermediateKeys</value>.
            /// </summary>
            /// <param name="cacheIntermediateKeys">whether to cache intermediate keys.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithCanCacheIntermediateKeys(bool cacheIntermediateKeys);

            /// <summary>
            /// Specifies whether to cache sessions. Defaults to <value>DefaultCanCacheSessions</value>.
            /// </summary>
            /// <param name="cacheSessions">whether to cache sessions.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithCanCacheSessions(bool cacheSessions);

            /// <summary>
            /// Specifies the session cache max size to use if session caching is enabled. Defaults to
            /// <value>DefaultSessionCacheSize</value>.
            /// </summary>
            /// <param name="sessionCacheMaxSize">the session cache max size to use.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithSessionCacheMaxSize(long sessionCacheMaxSize);

            /// <summary>
            /// Specifies the session cache expiration in minutes if session caching is enabled. Defaults to
            /// <value>DefaultSessionCacheExpiryMillis</value>.
            /// </summary>
            /// <param name="sessionCacheExpireMillis">the session cache expiration to use, in minutes.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithSessionCacheExpireMillis(long sessionCacheExpireMillis);

            /// <summary>
            /// Specifies whether to notify when expired system keys are read. Defaults to
            /// <value>DefaultNotifyExpiredSystemKeyOnRead</value>.
            /// </summary>
            /// <param name="notify">whether to notify.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithNotifyExpiredSystemKeyOnRead(bool notify);

            /// <summary>
            /// Specifies whether to notify when expired intermediate keys are read. Defaults to
            /// <value>DefaultNotifyExpiredIntermediateKeyOnRead</value>.
            /// </summary>
            /// <param name="notify">whether to notify.</param>
            /// <returns>The current <code>IBuildStep</code> instance.</returns>
            IBuildStep WithNotifyExpiredIntermediateKeyOnRead(bool notify);

            /// <summary>
            /// Builds the finalized <code>BasicExpiringCryptoPolicy</code> with the parameters specified in the builder.
            /// </summary>
            /// <returns>The fully instantiated <code>BasicExpiringCryptoPolicy</code>.</returns>
            BasicExpiringCryptoPolicy Build();
        }

        /// <summary>
        /// Initialize a <see cref="BasicExpiringCryptoPolicy"/> builder.
        /// </summary>
        ///
        /// <returns>The current <see cref="IKeyExpirationDaysStep"/> object.</returns>
        public static IKeyExpirationDaysStep NewBuilder()
        {
            return new Builder();
        }

        /// <inheritdoc />
        public override bool IsKeyExpired(DateTimeOffset keyCreationDate)
        {
            long currentUnixTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long expiredMillis = keyCreationDate.ToUnixTimeMilliseconds() + keyExpirationMillis;
            return currentUnixTimeMillis > expiredMillis;
        }

        /// <inheritdoc />
        public override long GetRevokeCheckPeriodMillis()
        {
            return revokeCheckMillis;
        }

        /// <inheritdoc />
        public override bool CanCacheSystemKeys()
        {
            return canCacheSystemKeys;
        }

        /// <inheritdoc />
        public override bool CanCacheIntermediateKeys()
        {
            return canCacheIntermediateKeys;
        }

        /// <inheritdoc />
        public override bool CanCacheSessions()
        {
            return canCacheSessions;
        }

        /// <inheritdoc />
        public override long GetSessionCacheMaxSize()
        {
            return sessionCacheMaxSize;
        }

        /// <inheritdoc />
        public override long GetSessionCacheExpireMillis()
        {
            return sessionCacheExpireMillis;
        }

        /// <inheritdoc />
        public override bool NotifyExpiredIntermediateKeyOnRead()
        {
            return notifyExpiredIntermediateKeyOnRead;
        }

        /// <inheritdoc />
        public override bool NotifyExpiredSystemKeyOnRead()
        {
            return notifyExpiredSystemKeyOnRead;
        }

        /// <inheritdoc />
        public override KeyRotationStrategy GetKeyRotationStrategy()
        {
            return keyRotationStrategy;
        }

        private class Builder : IKeyExpirationDaysStep, IRevokeCheckMinutesStep, IBuildStep
        {
            private int keyExpirationDays;
            private int revokeCheckMinutes;

            // Set some reasonable defaults since these aren't required by the builder steps
            private KeyRotationStrategy keyRotationStrategy = DefaultKeyRotationStrategy;
            private bool canCacheSystemKeys = DefaultCanCacheSystemKeys;
            private bool canCacheIntermediateKeys = DefaultCanCacheIntermediateKeys;
            private bool canCacheSessions = DefaultCanCacheSessions;
            private long sessionCacheMaxSize = DefaultSessionCacheSize;
            private long sessionCacheExpireMillis = DefaultSessionCacheExpiryMillis;
            private bool notifyExpiredSystemKeyOnRead = DefaultNotifyExpiredSystemKeyOnRead;
            private bool notifyExpiredIntermediateKeyOnRead = DefaultNotifyExpiredIntermediateKeyOnRead;

            // Internal properties for access
            internal int KeyExpirationDays => keyExpirationDays;
            internal int RevokeCheckMinutes => revokeCheckMinutes;
            internal KeyRotationStrategy KeyRotationStrategy => keyRotationStrategy;
            internal bool CanCacheSystemKeys => canCacheSystemKeys;
            internal bool CanCacheIntermediateKeys => canCacheIntermediateKeys;
            internal bool CanCacheSessions => canCacheSessions;
            internal long SessionCacheMaxSize => sessionCacheMaxSize;
            internal long SessionCacheExpireMillis => sessionCacheExpireMillis;
            internal bool NotifyExpiredSystemKeyOnRead => notifyExpiredSystemKeyOnRead;
            internal bool NotifyExpiredIntermediateKeyOnRead => notifyExpiredIntermediateKeyOnRead;

            private const KeyRotationStrategy DefaultKeyRotationStrategy = KeyRotationStrategy.Inline;
            private const bool DefaultCanCacheSystemKeys = true;
            private const bool DefaultCanCacheIntermediateKeys = true;
            private const bool DefaultCanCacheSessions = false;
            private const long DefaultSessionCacheSize = 1000;
            private const long DefaultSessionCacheExpiryMillis = 120000;
            private const bool DefaultNotifyExpiredSystemKeyOnRead = false;
            private const bool DefaultNotifyExpiredIntermediateKeyOnRead = false;

            public IRevokeCheckMinutesStep WithKeyExpirationDays(int days)
            {
                keyExpirationDays = days;
                return this;
            }

            public IBuildStep WithRevokeCheckMinutes(int minutes)
            {
                revokeCheckMinutes = minutes;
                return this;
            }

            public IBuildStep WithRotationStrategy(KeyRotationStrategy rotationStrategy)
            {
                keyRotationStrategy = rotationStrategy;
                return this;
            }

            public IBuildStep WithCanCacheSystemKeys(bool cacheSystemKeys)
            {
                canCacheSystemKeys = cacheSystemKeys;
                return this;
            }

            public IBuildStep WithCanCacheIntermediateKeys(bool cacheIntermediateKeys)
            {
                canCacheIntermediateKeys = cacheIntermediateKeys;
                return this;
            }

            public IBuildStep WithCanCacheSessions(bool cacheSessions)
            {
                canCacheSessions = cacheSessions;
                return this;
            }

            public IBuildStep WithSessionCacheMaxSize(long sessionCacheMaxSize)
            {
                this.sessionCacheMaxSize = sessionCacheMaxSize;
                return this;
            }

            public IBuildStep WithSessionCacheExpireMillis(long sessionCacheExpireMillis)
            {
                this.sessionCacheExpireMillis = sessionCacheExpireMillis;
                return this;
            }

            public IBuildStep WithNotifyExpiredSystemKeyOnRead(bool notify)
            {
                notifyExpiredSystemKeyOnRead = notify;
                return this;
            }

            public IBuildStep WithNotifyExpiredIntermediateKeyOnRead(bool notify)
            {
                notifyExpiredIntermediateKeyOnRead = notify;
                return this;
            }

            public BasicExpiringCryptoPolicy Build()
            {
                return new BasicExpiringCryptoPolicy(this);
            }
        }
    }
}

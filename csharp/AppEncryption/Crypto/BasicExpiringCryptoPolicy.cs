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
            IRevokeCheckMinutesStep WithKeyExpirationDays(int days);
        }

        public interface IRevokeCheckMinutesStep
        {
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

        public static IKeyExpirationDaysStep NewBuilder()
        {
            return new Builder();
        }

        public override bool IsKeyExpired(DateTimeOffset keyCreationDate)
        {
            long currentUnixTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long expiredMillis = keyCreationDate.ToUnixTimeMilliseconds() + keyExpirationMillis;
            return currentUnixTimeMillis > expiredMillis;
        }

        public override long GetRevokeCheckPeriodMillis()
        {
            return revokeCheckMillis;
        }

        public override bool CanCacheSystemKeys()
        {
            return canCacheSystemKeys;
        }

        public override bool CanCacheIntermediateKeys()
        {
            return canCacheIntermediateKeys;
        }

        public override bool CanCacheSessions()
        {
            return canCacheSessions;
        }

        public override long GetSessionCacheMaxSize()
        {
            return sessionCacheMaxSize;
        }

        public override long GetSessionCacheExpireMillis()
        {
            return sessionCacheExpireMillis;
        }

        public override bool NotifyExpiredIntermediateKeyOnRead()
        {
            return notifyExpiredIntermediateKeyOnRead;
        }

        public override bool NotifyExpiredSystemKeyOnRead()
        {
            return notifyExpiredSystemKeyOnRead;
        }

        public override KeyRotationStrategy GetKeyRotationStrategy()
        {
            return keyRotationStrategy;
        }

        private class Builder : IKeyExpirationDaysStep, IRevokeCheckMinutesStep, IBuildStep
        {
            #pragma warning disable SA1401
            internal int KeyExpirationDays;
            internal int RevokeCheckMinutes;

            // Set some reasonable defaults since these aren't required by the builder steps
            internal KeyRotationStrategy KeyRotationStrategy = DefaultKeyRotationStrategy;
            internal bool CanCacheSystemKeys = DefaultCanCacheSystemKeys;
            internal bool CanCacheIntermediateKeys = DefaultCanCacheIntermediateKeys;
            internal bool CanCacheSessions = DefaultCanCacheSessions;
            internal long SessionCacheMaxSize = DefaultSessionCacheSize;
            internal long SessionCacheExpireMillis = DefaultSessionCacheExpiryMillis;
            internal bool NotifyExpiredSystemKeyOnRead = DefaultNotifyExpiredSystemKeyOnRead;
            internal bool NotifyExpiredIntermediateKeyOnRead = DefaultNotifyExpiredIntermediateKeyOnRead;
            #pragma warning restore SA1401

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
                KeyExpirationDays = days;
                return this;
            }

            public IBuildStep WithRevokeCheckMinutes(int minutes)
            {
                RevokeCheckMinutes = minutes;
                return this;
            }

            public IBuildStep WithRotationStrategy(KeyRotationStrategy rotationStrategy)
            {
                KeyRotationStrategy = rotationStrategy;
                return this;
            }

            public IBuildStep WithCanCacheSystemKeys(bool cacheSystemKeys)
            {
                CanCacheSystemKeys = cacheSystemKeys;
                return this;
            }

            public IBuildStep WithCanCacheIntermediateKeys(bool cacheIntermediateKeys)
            {
                CanCacheIntermediateKeys = cacheIntermediateKeys;
                return this;
            }

            public IBuildStep WithCanCacheSessions(bool cacheSessions)
            {
                CanCacheSessions = cacheSessions;
                return this;
            }

            public IBuildStep WithSessionCacheMaxSize(long sessionCacheMaxSize)
            {
                SessionCacheMaxSize = sessionCacheMaxSize;
                return this;
            }

            public IBuildStep WithSessionCacheExpireMillis(long sessionCacheExpireMillis)
            {
                SessionCacheExpireMillis = sessionCacheExpireMillis;
                return this;
            }

            public IBuildStep WithNotifyExpiredSystemKeyOnRead(bool notify)
            {
                NotifyExpiredSystemKeyOnRead = notify;
                return this;
            }

            public IBuildStep WithNotifyExpiredIntermediateKeyOnRead(bool notify)
            {
                NotifyExpiredIntermediateKeyOnRead = notify;
                return this;
            }

            public BasicExpiringCryptoPolicy Build()
            {
                return new BasicExpiringCryptoPolicy(this);
            }
        }
    }
}

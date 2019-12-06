using System;

namespace GoDaddy.Asherah.Crypto
{
    /// <summary>
    /// A Crypto Policy that allows easy customization of the expiration duration and caching TTL,
    /// with default values for key strategy, caching, and notification options:
    /// <para>
    ///   - Key Rotation Strategy: Inline<br/>
    ///   - Caching of System and Intermediate Keys is allowed<br/>
    ///   - Shared Intermediate Key Cache is disabled<br/>
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
            sessionCacheExpireMillis = (int)TimeSpan.FromMinutes(builder.SessionCacheExpireMinutes).TotalMilliseconds;
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
            IBuildStep WithRotationStrategy(KeyRotationStrategy rotationStrategy);

            IBuildStep WithCanCacheSystemKeys(bool cacheSystemKeys);

            IBuildStep WithCanCacheIntermediateKeys(bool cacheIntermediateKeys);

            IBuildStep WithCanCacheSessions(bool cacheSessions);

            IBuildStep WithSessionCacheMaxSize(long sessionCacheMaxSize);

            IBuildStep WithSessionCacheExpireMinutes(int sessionExpireMinutes);

            IBuildStep WithNotifyExpiredSystemKeyOnRead(bool notify);

            IBuildStep WithNotifyExpiredIntermediateKeyOnRead(bool notify);

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
            internal KeyRotationStrategy KeyRotationStrategy = KeyRotationStrategy.Inline;
            internal bool CanCacheSystemKeys = true;
            internal bool CanCacheIntermediateKeys = true;
            internal bool CanCacheSessions = false;
            internal long SessionCacheMaxSize = DefaultSessionCacheSize;
            internal int SessionCacheExpireMinutes = 120;
            internal bool NotifyExpiredSystemKeyOnRead = false;
            internal bool NotifyExpiredIntermediateKeyOnRead = false;
            #pragma warning restore SA1401

            private const long DefaultSessionCacheSize = 1000;

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

            public IBuildStep WithSessionCacheExpireMinutes(int sessionExpireMinutes)
            {
                SessionCacheExpireMinutes = sessionExpireMinutes;
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

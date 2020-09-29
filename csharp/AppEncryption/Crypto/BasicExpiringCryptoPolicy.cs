using System;
using Microsoft.Extensions.Configuration;

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

        private BasicExpiringCryptoPolicy(ExpiringCryptoPolicyConfig policyConfig, IConfiguration configuration)
            : base(configuration)
        {
            keyExpirationMillis = (long)TimeSpan.FromDays(policyConfig.KeyExpirationDays).TotalMilliseconds;
            revokeCheckMillis = (long)TimeSpan.FromMinutes(policyConfig.RevokeCheckMinutes).TotalMilliseconds;
            keyRotationStrategy = policyConfig.KeyRotationStrategy;
            canCacheSystemKeys = policyConfig.CanCacheSystemKeys;
            canCacheIntermediateKeys = policyConfig.CanCacheIntermediateKeys;
            canCacheSessions = policyConfig.CanCacheSessions;
            sessionCacheMaxSize = policyConfig.SessionCacheMaxSize;
            sessionCacheExpireMillis = policyConfig.SessionCacheExpireMillis;
            notifyExpiredSystemKeyOnRead = policyConfig.NotifyExpiredSystemKeyOnRead;
            notifyExpiredIntermediateKeyOnRead = policyConfig.NotifyExpiredIntermediateKeyOnRead;
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

            IBuildStep WithConfiguration(IConfiguration configuration);

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

        public static BasicExpiringCryptoPolicy BuildWithConfiguration(IConfiguration configuration)
        {
            return new BasicExpiringCryptoPolicy(new ExpiringCryptoPolicyConfig(configuration), configuration);
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

        internal class ExpiringCryptoPolicyConfig
        {
            #pragma warning disable SA1401 // Fields should be private
            internal int KeyExpirationDays;
            internal int RevokeCheckMinutes;

            internal KeyRotationStrategy KeyRotationStrategy = DefaultKeyRotationStrategy;
            internal bool CanCacheSystemKeys = DefaultCanCacheSystemKeys;
            internal bool CanCacheIntermediateKeys = DefaultCanCacheIntermediateKeys;
            internal bool CanCacheSessions = DefaultCanCacheSessions;
            internal long SessionCacheMaxSize = DefaultSessionCacheSize;
            internal long SessionCacheExpireMillis = DefaultSessionCacheExpiryMillis;
            internal bool NotifyExpiredSystemKeyOnRead = DefaultNotifyExpiredSystemKeyOnRead;
            internal bool NotifyExpiredIntermediateKeyOnRead = DefaultNotifyExpiredIntermediateKeyOnRead;
            #pragma warning restore SA1401 // Fields should be private

            private const KeyRotationStrategy DefaultKeyRotationStrategy = KeyRotationStrategy.Inline;
            private const bool DefaultCanCacheSystemKeys = true;
            private const bool DefaultCanCacheIntermediateKeys = true;
            private const bool DefaultCanCacheSessions = false;
            private const long DefaultSessionCacheSize = 1000;
            private const long DefaultSessionCacheExpiryMillis = 120000;
            private const bool DefaultNotifyExpiredSystemKeyOnRead = false;
            private const bool DefaultNotifyExpiredIntermediateKeyOnRead = false;

            private IConfiguration configuration;

            internal ExpiringCryptoPolicyConfig()
            {
            }

            internal ExpiringCryptoPolicyConfig(IConfiguration configuration)
            {
                if (configuration == null)
                {
                    throw new ArgumentNullException(nameof(configuration), "Cannot create expiring crypto policy with null configuration");
                }

                this.configuration = configuration;

                KeyExpirationDays = RequiredInt("keyExpirationDays");
                RevokeCheckMinutes = RequiredInt("revokeCheckMinutes");

                var strategy = OptionalString("keyRotationStrategy", "inline");
                switch (strategy)
                {
                    case "inline":
                        KeyRotationStrategy = KeyRotationStrategy.Inline;
                        break;
                    case "queued":
                        KeyRotationStrategy = KeyRotationStrategy.Queued;
                        break;
                    default:
                        throw new Exception($"Unknown key rotation strategy {strategy}");
                }

                CanCacheSystemKeys = OptionalBool("canCacheSystemKeys", DefaultCanCacheSystemKeys);
                CanCacheIntermediateKeys = OptionalBool("canCacheIntermediateKeys", DefaultCanCacheIntermediateKeys);
                CanCacheSessions = OptionalBool("canCacheSessions", DefaultCanCacheSessions);
                SessionCacheMaxSize = OptionalLong("sessionCacheMaxSize", DefaultSessionCacheSize);
                SessionCacheExpireMillis = OptionalLong("sessionCacheExpireMillis", DefaultSessionCacheExpiryMillis);
                NotifyExpiredSystemKeyOnRead = OptionalBool("notifyExpiredSystemKeyOnRead", DefaultNotifyExpiredSystemKeyOnRead);
                NotifyExpiredIntermediateKeyOnRead = OptionalBool("notifyExpiredIntermediateKeyOnRead", DefaultNotifyExpiredIntermediateKeyOnRead);
            }

            private int RequiredInt(string name)
            {
                var val = configuration[name];
                if (string.IsNullOrWhiteSpace(val))
                {
                    throw new Exception($"Required configuration value {val} not found");
                }
                else
                {
                    return int.Parse(val);
                }
            }

            private string OptionalString(string name, string defaultValue)
            {
                var val = configuration[name];
                if (string.IsNullOrWhiteSpace(val))
                {
                    return defaultValue;
                }
                else
                {
                    return val;
                }
            }

            private long OptionalLong(string name, long defaultValue)
            {
                var val = configuration[name];
                if (string.IsNullOrWhiteSpace(val))
                {
                    return defaultValue;
                }
                else
                {
                    return long.Parse(val);
                }
            }

            private bool OptionalBool(string name, bool defaultValue)
            {
                var val = configuration[name];
                if (string.IsNullOrWhiteSpace(val))
                {
                    return defaultValue;
                }
                else
                {
                    return bool.Parse(val);
                }
            }
        }

        private class Builder : ExpiringCryptoPolicyConfig, IKeyExpirationDaysStep, IRevokeCheckMinutesStep, IBuildStep
        {
            private IConfiguration configuration;

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

            public IBuildStep WithConfiguration(IConfiguration configuration)
            {
                this.configuration = configuration;
                return this;
            }

            public BasicExpiringCryptoPolicy Build()
            {
                return new BasicExpiringCryptoPolicy(this, configuration);
            }
        }
    }
}

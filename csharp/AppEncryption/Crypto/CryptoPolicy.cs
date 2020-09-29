using System;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.Crypto
{
    public abstract class CryptoPolicy
    {
        private readonly Func<AeadEnvelopeCrypto> generateCrypto;

        protected CryptoPolicy(IConfiguration configuration)
        {
            Func<AeadEnvelopeCrypto> defaultGenerateCrypto = () => new BouncyAes256GcmCrypto(configuration);

            if (configuration != null)
            {
                switch (configuration["cryptoEngine"])
                {
                    case "Bouncy":
                        switch (configuration["cipher"])
                        {
                            case "aes-256-gcm":
                                generateCrypto = () => new BouncyAes256GcmCrypto(configuration);
                                break;
                            case null:
                            case "":
                                generateCrypto = defaultGenerateCrypto;
                                break;
                            default:
                                throw new CipherNotSupportedException("Unknown cipher: " + configuration["cipher"]);
                        }

                        break;
                    case null:
                    case "":
                        generateCrypto = defaultGenerateCrypto;
                        break;
                    default:
                        throw new Exception("Unknown cryptoEngine: " + configuration["cryptoEngine"]);
                }
            }
            else
            {
                // TODO: Should we allow configuration to be null?
                generateCrypto = defaultGenerateCrypto;
            }
        }

        public enum KeyRotationStrategy
        {
            Inline,
            Queued,
        }

        public abstract bool IsKeyExpired(DateTimeOffset keyCreationDate);

        public abstract long GetRevokeCheckPeriodMillis();

        public abstract bool CanCacheSystemKeys();

        public abstract bool CanCacheIntermediateKeys();

        public abstract bool CanCacheSessions();

        public abstract long GetSessionCacheMaxSize();

        public abstract long GetSessionCacheExpireMillis();

        public abstract bool NotifyExpiredIntermediateKeyOnRead();

        public abstract bool NotifyExpiredSystemKeyOnRead();

        public abstract KeyRotationStrategy GetKeyRotationStrategy();

        public virtual bool IsInlineKeyRotation()
        {
            return GetKeyRotationStrategy() == KeyRotationStrategy.Inline;
        }

        public virtual bool IsQueuedKeyRotation()
        {
            return GetKeyRotationStrategy() == KeyRotationStrategy.Queued;
        }

        public virtual DateTimeOffset TruncateToSystemKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromMinutes(1));
        }

        public virtual DateTimeOffset TruncateToIntermediateKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromMinutes(1));
        }

        public AeadEnvelopeCrypto GetCrypto()
        {
            return generateCrypto();
        }
    }
}

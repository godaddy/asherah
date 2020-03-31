using System;
using GoDaddy.Asherah.Crypto.ExtensionMethods;

namespace GoDaddy.Asherah.Crypto
{
    public abstract class CryptoPolicy
    {
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
    }
}

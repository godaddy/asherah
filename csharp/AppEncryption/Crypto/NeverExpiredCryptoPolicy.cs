using System;

namespace GoDaddy.Asherah.Crypto
{
    public class NeverExpiredCryptoPolicy : CryptoPolicy
    {
        public override bool IsKeyExpired(DateTimeOffset keyCreationDate)
        {
            return false;
        }

        public override long GetRevokeCheckPeriodMillis()
        {
            return long.MaxValue;
        }

        public override bool CanCacheSystemKeys()
        {
            return true;
        }

        public override bool CanCacheIntermediateKeys()
        {
            return true;
        }

        public override bool CanCacheSessions()
        {
            return false;
        }

        public override long GetSessionCacheMaxSize()
        {
            return long.MaxValue;
        }

        public override long GetSessionCacheExpireMillis()
        {
            return long.MaxValue;
        }

        public override bool NotifyExpiredIntermediateKeyOnRead()
        {
            return true;
        }

        public override bool NotifyExpiredSystemKeyOnRead()
        {
            return true;
        }

        public override KeyRotationStrategy GetKeyRotationStrategy()
        {
            return KeyRotationStrategy.Inline;
        }
    }
}

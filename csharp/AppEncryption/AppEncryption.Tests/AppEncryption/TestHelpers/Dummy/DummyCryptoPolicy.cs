using System;
using GoDaddy.Asherah.Crypto;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy
{
    public class DummyCryptoPolicy : CryptoPolicy
    {
        public override bool IsKeyExpired(DateTimeOffset keyCreationTime)
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

        public override bool IsInlineKeyRotation()
        {
            return true;
        }

        public override bool IsQueuedKeyRotation()
        {
            return false;
        }

        public override string ToString()
        {
            return typeof(DummyCryptoPolicy).FullName + "[policy=NeverExpire]";
        }
    }
}

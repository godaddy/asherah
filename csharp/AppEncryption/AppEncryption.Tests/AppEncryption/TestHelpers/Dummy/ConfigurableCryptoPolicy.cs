using System;
using System.Collections.Generic;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.ExtensionMethods;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy
{
    /// <summary>
    /// A crypto policy for testing that allows configurable expiration behavior.
    /// This enables testing inline rotation without waiting for real time to pass.
    /// Uses millisecond precision for key timestamps to avoid duplicate key issues in fast tests.
    /// </summary>
    public class ConfigurableCryptoPolicy : CryptoPolicy
    {
        private readonly HashSet<DateTimeOffset> _expiredKeyTimestamps = new();
        private bool _allKeysExpired;
        private readonly bool _canCacheSystemKeys;
        private readonly bool _canCacheIntermediateKeys;

        public ConfigurableCryptoPolicy(bool canCacheSystemKeys = false, bool canCacheIntermediateKeys = false)
        {
            _canCacheSystemKeys = canCacheSystemKeys;
            _canCacheIntermediateKeys = canCacheIntermediateKeys;
        }

        /// <summary>
        /// Marks a specific key timestamp as expired.
        /// </summary>
        public void MarkKeyAsExpired(DateTimeOffset keyCreated)
        {
            _expiredKeyTimestamps.Add(keyCreated);
        }

        /// <summary>
        /// Marks all keys as expired.
        /// </summary>
        public void MarkAllKeysAsExpired()
        {
            _allKeysExpired = true;
        }

        /// <summary>
        /// Clears all expiration markers.
        /// </summary>
        public void ClearExpirations()
        {
            _expiredKeyTimestamps.Clear();
            _allKeysExpired = false;
        }

        public override bool IsKeyExpired(DateTimeOffset keyCreationDate)
        {
            if (_allKeysExpired)
            {
                return true;
            }

            return _expiredKeyTimestamps.Contains(keyCreationDate);
        }

        public override long GetRevokeCheckPeriodMillis() => long.MaxValue;

        public override bool CanCacheSystemKeys() => _canCacheSystemKeys;

        public override bool CanCacheIntermediateKeys() => _canCacheIntermediateKeys;

        public override bool CanCacheSessions() => false;

        public override long GetSessionCacheMaxSize() => long.MaxValue;

        public override long GetSessionCacheExpireMillis() => long.MaxValue;

        public override bool NotifyExpiredIntermediateKeyOnRead() => false;

        public override bool NotifyExpiredSystemKeyOnRead() => false;

        public override KeyRotationStrategy GetKeyRotationStrategy() => KeyRotationStrategy.Inline;

        public override bool IsInlineKeyRotation() => true;

        public override bool IsQueuedKeyRotation() => false;

        /// <summary>
        /// Uses second precision instead of minute precision.
        /// This avoids duplicate key issues in fast-running tests.
        /// </summary>
        public override DateTimeOffset TruncateToSystemKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Uses second precision instead of minute precision.
        /// This avoids duplicate key issues in fast-running tests.
        /// </summary>
        public override DateTimeOffset TruncateToIntermediateKeyPrecision(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Truncate(TimeSpan.FromSeconds(1));
        }
    }
}

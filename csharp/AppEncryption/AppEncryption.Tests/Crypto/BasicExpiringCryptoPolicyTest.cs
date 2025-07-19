using System;
using GoDaddy.Asherah.Crypto;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto
{
    [Collection("Logger Fixture collection")]
    public class BasicExpiringCryptoPolicyTest
    {
        private static readonly int TestExpirationDays = 2;
        private static readonly int TestCachingPeriod = 30;

        private readonly BasicExpiringCryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(TestExpirationDays)
            .WithRevokeCheckMinutes(TestCachingPeriod)
            .Build();

        [Fact]
        private void TestIsKeyExpired()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset before = now.AddDays(-3);

            Assert.True(policy.IsKeyExpired(before));
        }

        [Fact]
        private void TestKeyIsNotExpired()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset before = now.AddDays(-1);

            Assert.False(policy.IsKeyExpired(before));
        }

        [Fact]
        private void TestRevokeCheckMillis()
        {
            Assert.Equal(
                TimeSpan.FromMinutes(TestCachingPeriod).TotalMilliseconds,
                policy.GetRevokeCheckPeriodMillis());
        }

        [Fact]
        private void TestDefaultsDontChange()
        {
            Assert.True(policy.CanCacheSystemKeys());
            Assert.True(policy.CanCacheIntermediateKeys());
            Assert.False(policy.CanCacheSessions());
            Assert.Equal(1000, policy.GetSessionCacheMaxSize());
            Assert.Equal(2 * 60 * 1000, policy.GetSessionCacheExpireMillis());
            Assert.Equal(CryptoPolicy.KeyRotationStrategy.Inline, policy.GetKeyRotationStrategy());
            Assert.False(policy.NotifyExpiredSystemKeyOnRead());
            Assert.False(policy.NotifyExpiredIntermediateKeyOnRead());
        }

        [Fact]
        private void TestPrimaryBuilderPath()
        {
            BasicExpiringCryptoPolicy.IKeyExpirationDaysStep builder = BasicExpiringCryptoPolicy.NewBuilder();

            BasicExpiringCryptoPolicy basicExpiringCryptoPolicy = builder.
                WithKeyExpirationDays(TestExpirationDays).
                WithRevokeCheckMinutes(TestCachingPeriod).Build();
            Assert.NotNull(basicExpiringCryptoPolicy);
        }

        [Fact]
        private void TestFullBuilderPath()
        {
            BasicExpiringCryptoPolicy.IKeyExpirationDaysStep builder = BasicExpiringCryptoPolicy.NewBuilder();

            BasicExpiringCryptoPolicy basicExpiringCryptoPolicy = builder
                .WithKeyExpirationDays(TestExpirationDays)
                .WithRevokeCheckMinutes(TestCachingPeriod)
                .WithRotationStrategy(CryptoPolicy.KeyRotationStrategy.Queued)
                .WithCanCacheSystemKeys(false)
                .WithCanCacheIntermediateKeys(false)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(42)
                .WithSessionCacheExpireMillis(33000)
                .WithNotifyExpiredSystemKeyOnRead(true)
                .WithNotifyExpiredIntermediateKeyOnRead(true)
                .Build();

            Assert.NotNull(basicExpiringCryptoPolicy);
            Assert.Equal(CryptoPolicy.KeyRotationStrategy.Queued, basicExpiringCryptoPolicy.GetKeyRotationStrategy());
            Assert.False(basicExpiringCryptoPolicy.CanCacheSystemKeys());
            Assert.False(basicExpiringCryptoPolicy.CanCacheIntermediateKeys());
            Assert.True(basicExpiringCryptoPolicy.CanCacheSessions());
            Assert.Equal(42, basicExpiringCryptoPolicy.GetSessionCacheMaxSize());
            Assert.Equal(33000, basicExpiringCryptoPolicy.GetSessionCacheExpireMillis());
            Assert.True(basicExpiringCryptoPolicy.NotifyExpiredSystemKeyOnRead());
            Assert.True(basicExpiringCryptoPolicy.NotifyExpiredIntermediateKeyOnRead());
        }
    }
}

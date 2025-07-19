using System;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto
{
    [Collection("Logger Fixture collection")]
    public class CryptoPolicyTest
    {
        private readonly Mock<CryptoPolicy> cryptoPolicyMock;

        public CryptoPolicyTest()
        {
            cryptoPolicyMock = new Mock<CryptoPolicy>();
        }

        [Fact]
        private void IsInlineKeyRotation()
        {
            const CryptoPolicy.KeyRotationStrategy expectedStrategy = CryptoPolicy.KeyRotationStrategy.Inline;
            cryptoPolicyMock.Setup(x => x.GetKeyRotationStrategy()).Returns(expectedStrategy);
            cryptoPolicyMock.Setup(x => x.IsInlineKeyRotation()).CallBase();
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).CallBase();

            Assert.True(cryptoPolicyMock.Object.IsInlineKeyRotation());
            Assert.False(cryptoPolicyMock.Object.IsQueuedKeyRotation());
        }

        [Fact]
        private void IsQueuedKeyRotation()
        {
            const CryptoPolicy.KeyRotationStrategy expectedStrategy = CryptoPolicy.KeyRotationStrategy.Queued;
            cryptoPolicyMock.Setup(x => x.GetKeyRotationStrategy()).Returns(expectedStrategy);
            cryptoPolicyMock.Setup(x => x.IsInlineKeyRotation()).CallBase();
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).CallBase();

            Assert.True(cryptoPolicyMock.Object.IsQueuedKeyRotation());
            Assert.False(cryptoPolicyMock.Object.IsInlineKeyRotation());
        }

        [Fact]
        private void TestDefaultSystemKeyPrecision()
        {
            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).CallBase();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset actualTime = cryptoPolicyMock.Object.TruncateToSystemKeyPrecision(now);
            Assert.Equal(now.Truncate(TimeSpan.FromMinutes(1)), actualTime);
        }

        [Fact]
        private void TestDefaultIntermediateKeyPrecision()
        {
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).CallBase();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset actualTime = cryptoPolicyMock.Object.TruncateToIntermediateKeyPrecision(now);
            Assert.Equal(now.Truncate(TimeSpan.FromMinutes(1)), actualTime);
        }
    }
}

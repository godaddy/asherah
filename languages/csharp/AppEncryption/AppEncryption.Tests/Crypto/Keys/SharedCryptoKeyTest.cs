using System;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Keys
{
    [Collection("Logger Fixture collection")]
    public class SharedCryptoKeyTest
    {
        private readonly Mock<CryptoKey> sharedKeyMock;
        private readonly SharedCryptoKey sharedCryptoKey;

        public SharedCryptoKeyTest()
        {
            sharedKeyMock = new Mock<CryptoKey>();
            sharedCryptoKey = new SharedCryptoKey(sharedKeyMock.Object);
        }

        [Fact]
        public void TestConstructor()
        {
            SharedCryptoKey sharedCryptoKey = new SharedCryptoKey(sharedKeyMock.Object);
            Assert.NotNull(sharedCryptoKey);
        }

        [Fact]
        public void TestWithKeyFunction()
        {
            Func<byte[], object> action = keyBytes => null;
            sharedCryptoKey.WithKey(action);
            sharedKeyMock.Verify(x => x.WithKey(action), Times.Once);
        }

        [Fact]
        public void TestWithKeyAction()
        {
            Action<byte[]> action = bytes =>
            {
            };

            sharedCryptoKey.WithKey(action);
            sharedKeyMock.Verify(x => x.WithKey(action), Times.Once);
        }

        [Fact]
        public void TestGetCreated()
        {
            DateTimeOffset expectedCreationTime = DateTimeOffset.UtcNow;
            sharedKeyMock.Setup(x => x.GetCreated()).Returns(expectedCreationTime);
            DateTimeOffset actualCreatedTime = sharedCryptoKey.GetCreated();
            Assert.Equal(expectedCreationTime, actualCreatedTime);
        }

        [Fact]
        public void TestMarkRevoked()
        {
            sharedKeyMock.Setup(x => x.IsRevoked()).Returns(false);
            Assert.False(sharedCryptoKey.IsRevoked());
            sharedCryptoKey.MarkRevoked();
            sharedKeyMock.Verify(x => x.MarkRevoked(), Times.Once);
        }

        [Fact]
        public void TestDispose()
        {
            sharedCryptoKey.Dispose();
            sharedKeyMock.Verify(x => x.Dispose(), Times.Never);
        }
    }
}

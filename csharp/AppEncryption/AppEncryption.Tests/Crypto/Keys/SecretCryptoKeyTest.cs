using System;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Keys
{
    [Collection("Logger Fixture collection")]
    public class SecretCryptoKeyTest
    {
        private const bool Revoked = false;

        private readonly DateTimeOffset creationTime = DateTimeOffset.UtcNow;

        private readonly Mock<Secret> secretMock;
        private readonly SecretCryptoKey secretCryptoKey;

        public SecretCryptoKeyTest()
        {
            secretMock = new Mock<Secret> { CallBase = true };
            secretCryptoKey = new SecretCryptoKey(secretMock.Object, creationTime, Revoked);
        }

        [Fact]
        public void TestConstructor()
        {
            Assert.NotNull(secretCryptoKey);
        }

        [Fact]
        public void TestConstructorUsingOtherKey()
        {
            Mock<Secret> secretToCopy = new Mock<Secret> { CallBase = true };
            Mock<SecretCryptoKey> secretCryptoKeyMock = new Mock<SecretCryptoKey>(null, creationTime, Revoked);
            secretCryptoKeyMock.SetupGet(x => x.Secret).Returns(secretToCopy.Object);
            secretCryptoKeyMock.Setup(x => x.GetCreated()).Returns(creationTime);
            secretToCopy.Setup(x => x.CopySecret()).Returns(secretMock.Object);

            SecretCryptoKey secretCryptoKey = new SecretCryptoKey(secretCryptoKeyMock.Object);

            Assert.NotNull(secretCryptoKey);
            Assert.Equal(creationTime, secretCryptoKey.GetCreated());
            Assert.Equal(secretMock.Object, secretCryptoKey.Secret);
            Assert.False(secretCryptoKey.IsRevoked());
        }

        [Fact]
        public void TestKeyWithFunction()
        {
            Func<byte[], object> action = keyBytes => null;
            secretCryptoKey.WithKey(action);
            secretMock.Verify(x => x.WithSecretBytes(action), Times.Once);
        }

        [Fact]
        public void TestKeyWithAction()
        {
            Action<byte[]> action = keyBytes => { };
            secretCryptoKey.WithKey(action);
            secretMock.Verify(x => x.WithSecretBytes(action), Times.Once);
        }

        [Fact]
        public void TestGetCreated()
        {
            DateTimeOffset actualCreated = secretCryptoKey.GetCreated();
            Assert.Equal(creationTime, actualCreated);
        }

        [Fact]
        public void TestMarkRevoked()
        {
            Assert.False(secretCryptoKey.IsRevoked());
            secretCryptoKey.MarkRevoked();
            Assert.True(secretCryptoKey.IsRevoked());
        }

        [Fact]
        public void TestClose()
        {
            secretCryptoKey.Dispose();
            secretMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void TestGetSecret()
        {
            Secret actualSecret = secretCryptoKey.Secret;
            Assert.Equal(secretMock.Object, actualSecret);
        }
    }
}

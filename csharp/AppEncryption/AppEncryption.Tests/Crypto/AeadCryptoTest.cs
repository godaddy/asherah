using System;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.SecureMemory;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto
{
    [Collection("Logger Fixture collection")]
    public class AeadCryptoTest
    {
        private const int BitsPerByte = 8;
        private readonly Mock<AeadCrypto> aeadCryptoMock;
        private readonly Mock<CryptoKey> cryptoKeyMock;
        private readonly Mock<Secret> secretMock;
        private readonly Mock<ISecretFactory> secretFactoryMock;

        public AeadCryptoTest()
        {
            cryptoKeyMock = new Mock<CryptoKey>();
            aeadCryptoMock = new Mock<AeadCrypto>();
            secretMock = new Mock<Secret>();
            secretFactoryMock = new Mock<ISecretFactory>();
        }

        [Fact]
        private void TestGenerateKeyFromBytesByteArray()
        {
            byte[] sourceBytes = { 0, 1 };
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, sourceBytes, It.IsAny<DateTimeOffset>())).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, It.IsAny<byte[]>())).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(secretFactoryMock.Object, sourceBytes);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateKeyFromBytesByteArrayDateTimeOffset()
        {
            byte[] sourceBytes = { 0, 1 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, sourceBytes, now, false)).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>())).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(secretFactoryMock.Object, sourceBytes, now);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateRandomCryptoKeyCreatedWithInvalidKeySize()
        {
            aeadCryptoMock.Setup(x => x.GetKeySizeBits()).Returns(BitsPerByte + 1);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(secretFactoryMock.Object, It.IsAny<DateTimeOffset>())).CallBase();
            Assert.Throws<ArgumentException>(() => aeadCryptoMock.Object.GenerateRandomCryptoKey(secretFactoryMock.Object, DateTimeOffset.UtcNow));
        }

        [Fact]
        private void TestGenerateRandomCryptoKey()
        {
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(secretFactoryMock.Object, It.IsAny<DateTimeOffset>())).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(secretFactoryMock.Object)).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateRandomCryptoKey(secretFactoryMock.Object);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateRandomCryptoKeyCreatedWithValidKeySize()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            aeadCryptoMock.Setup(x => x.GetKeySizeBits()).Returns(BitsPerByte * 2);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, It.IsAny<byte[]>(), now)).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(secretFactoryMock.Object, It.IsAny<DateTimeOffset>())).CallBase();
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(secretFactoryMock.Object)).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateRandomCryptoKey(secretFactoryMock.Object, now);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateKeyFromBytesByteArrayInstantBoolean()
        {
            byte[] sourceBytes = { 2, 3 };
            byte[] clearedBytes = { 0, 0 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool revoked = true;
            secretFactoryMock.Setup(x => x.CreateSecret(sourceBytes)).Returns(secretMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(secretFactoryMock.Object, It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>())).CallBase();

            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(secretFactoryMock.Object, sourceBytes, now, revoked);
            Assert.Equal(typeof(SecretCryptoKey), actualCryptoKey.GetType());
            Assert.Equal(now, actualCryptoKey.GetCreated());
            Assert.Equal(revoked, actualCryptoKey.IsRevoked());
            Assert.NotEqual(clearedBytes, sourceBytes);
            secretFactoryMock.Verify(x => x.CreateSecret(sourceBytes), Times.Once);
        }
    }
}

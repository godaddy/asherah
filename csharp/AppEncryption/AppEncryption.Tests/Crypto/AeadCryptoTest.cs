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
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(sourceBytes, It.IsAny<DateTimeOffset>())).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(It.IsAny<byte[]>())).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(sourceBytes);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateKeyFromBytesByteArrayDateTimeOffset()
        {
            byte[] sourceBytes = { 0, 1 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(sourceBytes, now, false)).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>())).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(sourceBytes, now);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateRandomCryptoKeyCreatedWithInvalidKeySize()
        {
            aeadCryptoMock.Setup(x => x.GetKeySizeBits()).Returns(BitsPerByte + 1);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(It.IsAny<DateTimeOffset>())).CallBase();
            Assert.Throws<ArgumentException>(() => aeadCryptoMock.Object.GenerateRandomCryptoKey(DateTimeOffset.UtcNow));
        }

        [Fact]
        private void TestGenerateRandomCryptoKey()
        {
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(It.IsAny<DateTimeOffset>())).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey()).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateRandomCryptoKey();
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateRandomCryptoKeyCreatedWithValidKeySize()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            aeadCryptoMock.Setup(x => x.GetKeySizeBits()).Returns(BitsPerByte * 2);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(It.IsAny<byte[]>(), now)).Returns(cryptoKeyMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey(It.IsAny<DateTimeOffset>())).CallBase();
            aeadCryptoMock.Setup(x => x.GenerateRandomCryptoKey()).CallBase();
            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateRandomCryptoKey(now);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestGenerateKeyFromBytesByteArrayInstantBoolean()
        {
            byte[] sourceBytes = { 2, 3 };
            byte[] clearedBytes = { 0, 0 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool revoked = true;
            aeadCryptoMock.Setup(x => x.GetSecretFactory()).Returns(secretFactoryMock.Object);
            secretFactoryMock.Setup(x => x.CreateSecret(sourceBytes)).Returns(secretMock.Object);
            aeadCryptoMock.Setup(x => x.GenerateKeyFromBytes(It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>())).CallBase();

            CryptoKey actualCryptoKey = aeadCryptoMock.Object.GenerateKeyFromBytes(sourceBytes, now, revoked);
            Assert.Equal(typeof(SecretCryptoKey), actualCryptoKey.GetType());
            Assert.Equal(now, actualCryptoKey.GetCreated());
            Assert.Equal(revoked, actualCryptoKey.IsRevoked());
            Assert.NotEqual(clearedBytes, sourceBytes);
            secretFactoryMock.Verify(x => x.CreateSecret(sourceBytes), Times.Once);
        }
    }
}

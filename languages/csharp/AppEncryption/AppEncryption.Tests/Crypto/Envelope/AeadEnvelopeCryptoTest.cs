using System;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Envelope
{
    [Collection("Logger Fixture collection")]
    public class AeadEnvelopeCryptoTest
    {
        private readonly Mock<CryptoKey> keyEncryptionKey;
        private readonly Mock<CryptoKey> keyMock;
        private readonly Mock<AeadEnvelopeCrypto> aeadEnvelopeCryptoMock;

        public AeadEnvelopeCryptoTest()
        {
            keyEncryptionKey = new Mock<CryptoKey>();
            keyMock = new Mock<CryptoKey>();
            aeadEnvelopeCryptoMock = new Mock<AeadEnvelopeCrypto>();
        }

        [Fact]
        private void TestEncryptKey()
        {
            byte[] keyBytes = { 0, 1, 2, 3 };
            byte[] expectedEncryptedKey = { 4, 5, 6, 7 };

            keyMock.Setup(x => x.WithKey(It.IsAny<Func<byte[], byte[]>>()))
                .Returns<Func<byte[], byte[]>>(action => action(keyBytes));
            aeadEnvelopeCryptoMock.Setup(x => x.Encrypt(keyBytes, It.IsAny<CryptoKey>())).Returns(expectedEncryptedKey);
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>())).CallBase();

            byte[] actualEncryptedKey =
                aeadEnvelopeCryptoMock.Object.EncryptKey(keyMock.Object, keyEncryptionKey.Object);
            Assert.Equal(expectedEncryptedKey, actualEncryptedKey);
            keyMock.Verify(x => x.WithKey(It.IsAny<Func<byte[], byte[]>>()));
            aeadEnvelopeCryptoMock.Verify(x => x.Encrypt(keyBytes, keyEncryptionKey.Object));
        }

        [Fact]
        private void TestDecryptKey()
        {
            byte[] encryptedKey = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            DateTimeOffset createdTime = DateTimeOffset.UtcNow;
            aeadEnvelopeCryptoMock.Setup(x => x.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object, false))
                .Returns(keyMock.Object);
            aeadEnvelopeCryptoMock.Setup(x => x.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object))
                .CallBase();

            CryptoKey actualKey =
                aeadEnvelopeCryptoMock.Object.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object);
            Assert.Equal(keyMock.Object, actualKey);
        }

        [Fact]
        private void TestDecryptKeyWithRevoked()
        {
            byte[] encryptedKey = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] decryptedKey = { 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] expectedFinalKey = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            DateTimeOffset createdTime = DateTimeOffset.UtcNow;
            bool revoked = true;

            aeadEnvelopeCryptoMock.Setup(x => x.Decrypt(encryptedKey, keyEncryptionKey.Object)).Returns(decryptedKey);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKeyFromBytes(decryptedKey, createdTime, revoked))
                .Returns(keyMock.Object);
            aeadEnvelopeCryptoMock.Setup(x => x.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object, revoked))
                .CallBase();
            Assert.NotEqual(string.Join(string.Empty, expectedFinalKey), string.Join(string.Empty, decryptedKey));

            CryptoKey actualKey =
                aeadEnvelopeCryptoMock.Object.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object, revoked);
            Assert.Equal(keyMock.Object, actualKey);
            Assert.Equal(expectedFinalKey, decryptedKey);
        }

        [Fact]
        private void TestEnvelopeEncrypt()
        {
            byte[] expectedEncryptedKey = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] expectedPlainText = { 1, 2, 3, 4, 5 };
            byte[] expectedCipherText = { 5, 4, 3, 2, 1 };

            aeadEnvelopeCryptoMock.Setup(x => x.Encrypt(expectedPlainText, keyMock.Object)).Returns(expectedCipherText);
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(keyMock.Object, keyEncryptionKey.Object))
                .Returns(expectedEncryptedKey);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey()).Returns(keyMock.Object);
            aeadEnvelopeCryptoMock.Setup(x => x.EnvelopeEncrypt(expectedPlainText, keyEncryptionKey.Object, null))
                .CallBase();

            EnvelopeEncryptResult result = aeadEnvelopeCryptoMock.Object.EnvelopeEncrypt(expectedPlainText, keyEncryptionKey.Object, null);
            Assert.Equal(expectedCipherText, result.CipherText);
            Assert.Equal(expectedEncryptedKey, result.EncryptedKey);
            Assert.Null(result.UserState);
            keyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestEnvelopeEncryptWithTwoParams()
        {
            byte[] encryptedKey = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            aeadEnvelopeCryptoMock.Setup(x => x.EnvelopeEncrypt(
                encryptedKey, keyEncryptionKey.Object)).CallBase();

            aeadEnvelopeCryptoMock.Object.EnvelopeEncrypt(encryptedKey, keyEncryptionKey.Object);
            aeadEnvelopeCryptoMock.Verify(x => x.EnvelopeEncrypt(encryptedKey, keyEncryptionKey.Object, null));
        }

        [Fact]
        private void TestEnvelopeDecrypt()
        {
            Mock<CryptoKey> plainTextMock = new Mock<CryptoKey>();

            byte[] cipherText = { 1, 2, 3, 4 };
            byte[] expectedBytes = { 5, 6, 7, 8 };
            byte[] encryptedKey = { 4, 5, 6, 7 };

            DateTimeOffset createdTime = DateTimeOffset.UtcNow;

            aeadEnvelopeCryptoMock.Setup(x => x.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object))
                .Returns(plainTextMock.Object);
            aeadEnvelopeCryptoMock.Setup(x => x.Decrypt(cipherText, plainTextMock.Object)).Returns(expectedBytes);
            aeadEnvelopeCryptoMock.Setup(x => x.EnvelopeDecrypt(cipherText, encryptedKey, createdTime, keyEncryptionKey.Object))
                .CallBase();

            byte[] actualBytes = aeadEnvelopeCryptoMock.Object.EnvelopeDecrypt(
                cipherText, encryptedKey, createdTime, keyEncryptionKey.Object);

            aeadEnvelopeCryptoMock.Verify(x => x.Decrypt(cipherText, plainTextMock.Object));
            aeadEnvelopeCryptoMock.Verify(x => x.DecryptKey(encryptedKey, createdTime, keyEncryptionKey.Object));
            Assert.Equal(expectedBytes, actualBytes);
            plainTextMock.Verify(x => x.Dispose());
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Kms
{
    [Collection("Logger Fixture collection")]
    public class KeyManagementServiceTest
    {
        private readonly Mock<CryptoKey> cryptoKeyMock;
        private readonly Mock<KeyManagementService> keyManagementServiceMock;

        public KeyManagementServiceTest()
        {
            cryptoKeyMock = new Mock<CryptoKey>();
            keyManagementServiceMock = new Mock<KeyManagementService>();
        }

        [Fact]
        private void TestWithDecryptedKey()
        {
            byte[] keyCipherText = { 0, 1 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            const bool revoked = false;
            const string expectedResult = "success";

            string ActionWithDecryptedKey(CryptoKey key, DateTimeOffset datetimeOffset)
            {
                if (cryptoKeyMock.Object.Equals(key) && now.Equals(datetimeOffset))
                {
                    return expectedResult;
                }

                return "failure";
            }

            keyManagementServiceMock.Setup(x => x.DecryptKey(keyCipherText, now, revoked))
                .Returns(cryptoKeyMock.Object);
            keyManagementServiceMock.Setup(x =>
                    x.WithDecryptedKey(It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<Func<CryptoKey, DateTimeOffset, string>>()))
                .CallBase();

            string actualResult = keyManagementServiceMock.Object.WithDecryptedKey(keyCipherText, now, revoked, ActionWithDecryptedKey);
            Assert.Equal(expectedResult, actualResult);
        }
    }
}

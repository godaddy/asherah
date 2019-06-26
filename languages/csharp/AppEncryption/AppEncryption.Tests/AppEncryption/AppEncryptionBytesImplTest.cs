using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class AppEncryptionBytesImplTest
    {
        private readonly Mock<IEnvelopeEncryption<string>> envelopeEncryptionMock;
        private readonly AppEncryptionBytesImpl<string> appEncryptionBytesImpl;

        public AppEncryptionBytesImplTest()
        {
            envelopeEncryptionMock = new Mock<IEnvelopeEncryption<string>>();
            appEncryptionBytesImpl = new AppEncryptionBytesImpl<string>(envelopeEncryptionMock.Object);
        }

        [Fact]
        private void TestConstructor()
        {
            AppEncryptionBytesImpl<string> appEncryption = new AppEncryptionBytesImpl<string>(envelopeEncryptionMock.Object);
            Assert.NotNull(appEncryption);
        }

        [Fact]
        private void TestDecrypt()
        {
            byte[] expectedBytes = { 0, 1, 2, 3, 4 };

            envelopeEncryptionMock.Setup(x => x.DecryptDataRowRecord(It.IsAny<string>())).Returns(expectedBytes);

            byte[] actualBytes = appEncryptionBytesImpl.Decrypt("some data row record");
            Assert.Equal(expectedBytes,  actualBytes);
        }

        [Fact]
        private void TestEncrypt()
        {
            const string expectedDataRowRecord = "some data row record";

            envelopeEncryptionMock.Setup(x => x.EncryptPayload(It.IsAny<byte[]>())).Returns(expectedDataRowRecord);

            string actualDataRowRecord = appEncryptionBytesImpl.Encrypt(new byte[] { 0, 1, 2, 3, 4 });
            Assert.Equal(expectedDataRowRecord, actualDataRowRecord);
        }

        [Fact]
        private void TestDispose()
        {
            appEncryptionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestCloseWithCloseFailShouldReturn()
        {
            envelopeEncryptionMock.Setup(x => x.Dispose()).Throws<SystemException>();
            appEncryptionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class SessionBytesImplTest
    {
        private readonly Mock<IEnvelopeEncryption<string>> envelopeEncryptionMock;
        private readonly SessionBytesImpl<string> sessionBytesImpl;

        public SessionBytesImplTest()
        {
            envelopeEncryptionMock = new Mock<IEnvelopeEncryption<string>>();
            sessionBytesImpl = new SessionBytesImpl<string>(envelopeEncryptionMock.Object);
        }

        [Fact]
        private void TestConstructor()
        {
            SessionBytesImpl<string> session = new SessionBytesImpl<string>(envelopeEncryptionMock.Object);
            Assert.NotNull(session);
        }

        [Fact]
        private void TestDecrypt()
        {
            byte[] expectedBytes = { 0, 1, 2, 3, 4 };

            envelopeEncryptionMock.Setup(x => x.DecryptDataRowRecord(It.IsAny<string>())).Returns(expectedBytes);

            byte[] actualBytes = sessionBytesImpl.Decrypt("some data row record");
            Assert.Equal(expectedBytes,  actualBytes);
        }

        [Fact]
        private void TestEncrypt()
        {
            const string expectedDataRowRecord = "some data row record";

            envelopeEncryptionMock.Setup(x => x.EncryptPayload(It.IsAny<byte[]>())).Returns(expectedDataRowRecord);

            string actualDataRowRecord = sessionBytesImpl.Encrypt(new byte[] { 0, 1, 2, 3, 4 });
            Assert.Equal(expectedDataRowRecord, actualDataRowRecord);
        }

        [Fact]
        private void TestDispose()
        {
            sessionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestCloseWithCloseFailShouldReturn()
        {
            envelopeEncryptionMock.Setup(x => x.Dispose()).Throws<SystemException>();
            sessionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }
    }
}

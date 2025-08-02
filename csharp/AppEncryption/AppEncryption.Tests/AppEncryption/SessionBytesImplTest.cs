using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class SessionBytesImplTest : IDisposable
    {
        private readonly Mock<IEnvelopeEncryption<string>> envelopeEncryptionMock;
        private readonly Mock<ILogger> mockLogger;
        private readonly SessionBytesImpl<string> sessionBytesImpl;

        public SessionBytesImplTest()
        {
            envelopeEncryptionMock = new Mock<IEnvelopeEncryption<string>>();
            mockLogger = new Mock<ILogger>();
            sessionBytesImpl = new SessionBytesImpl<string>(envelopeEncryptionMock.Object, mockLogger.Object);
        }

        [Fact]
        public void TestConstructor()
        {
            SessionBytesImpl<string> session = new SessionBytesImpl<string>(envelopeEncryptionMock.Object, mockLogger.Object);
            Assert.NotNull(session);
        }

        [Fact]
        public void TestDecrypt()
        {
            byte[] expectedBytes = { 0, 1, 2, 3, 4 };

            envelopeEncryptionMock.Setup(x => x.DecryptDataRowRecord(It.IsAny<string>())).Returns(expectedBytes);

            byte[] actualBytes = sessionBytesImpl.Decrypt("some data row record");
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void TestEncrypt()
        {
            const string expectedDataRowRecord = "some data row record";

            envelopeEncryptionMock.Setup(x => x.EncryptPayload(It.IsAny<byte[]>())).Returns(expectedDataRowRecord);

            string actualDataRowRecord = sessionBytesImpl.Encrypt(new byte[] { 0, 1, 2, 3, 4 });
            Assert.Equal(expectedDataRowRecord, actualDataRowRecord);
        }

        [Fact]
        public void TestDispose()
        {
            sessionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        [Fact]
        public void TestCloseWithCloseFailShouldReturn()
        {
            envelopeEncryptionMock.Setup(x => x.Dispose()).Throws<SystemException>();
            sessionBytesImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            sessionBytesImpl?.Dispose();
        }
    }
}

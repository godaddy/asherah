using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    public class SessionJsonImplTest : IDisposable
    {
        private readonly Mock<IEnvelopeEncryption<string>> envelopeEncryptionMock;
        private readonly Mock<ILogger> mockLogger;
        private readonly SessionJsonImpl<string> sessionJsonImpl;

        public SessionJsonImplTest()
        {
            envelopeEncryptionMock = new Mock<IEnvelopeEncryption<string>>();
            mockLogger = new Mock<ILogger>();
            sessionJsonImpl = new SessionJsonImpl<string>(envelopeEncryptionMock.Object, mockLogger.Object);
        }

        [Fact]
        public void TestConstructor()
        {
            SessionJsonImpl<string> session = new SessionJsonImpl<string>(envelopeEncryptionMock.Object, mockLogger.Object);
            Assert.NotNull(session);
        }

        [Fact]
        public void TestDecrypt()
        {
            const string json = @"{key:'some_key', value:123}";
            JObject expectedJson = JObject.Parse(json);
            byte[] utf8Bytes = new Asherah.AppEncryption.Util.Json(expectedJson).ToUtf8();

            envelopeEncryptionMock.Setup(x => x.DecryptDataRowRecord(It.IsAny<string>())).Returns(utf8Bytes);

            JObject actualJson = sessionJsonImpl.Decrypt("some data row record");
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void TestEncrypt()
        {
            const string expectedDataRowRecord = "some data row record";
            const string json = @"{key:'some_key', value:123}";
            JObject jObject = JObject.Parse(json);

            envelopeEncryptionMock.Setup(x => x.EncryptPayload(It.IsAny<byte[]>())).Returns(expectedDataRowRecord);

            string actualDataRowRecord = sessionJsonImpl.Encrypt(jObject);
            Assert.Equal(expectedDataRowRecord, actualDataRowRecord);
        }

        [Fact]
        public void TestDispose()
        {
            sessionJsonImpl.Dispose();

            // Verify proper resources are closed
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        [Fact]
        public void TestCloseWithCloseFailShouldReturn()
        {
            envelopeEncryptionMock.Setup(x => x.Dispose()).Throws<Exception>();
            sessionJsonImpl.Dispose();
            envelopeEncryptionMock.Verify(x => x.Dispose());
        }

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            sessionJsonImpl?.Dispose();
        }
    }
}

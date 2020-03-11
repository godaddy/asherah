using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Envelope
{
    [Collection("Logger Fixture collection")]
    public class EnvelopeEncryptionBytesImplTest
    {
        private readonly Mock<EnvelopeEncryptionJsonImpl> envelopeEncryptionJsonImplMock;
        private readonly EnvelopeEncryptionBytesImpl envelopeEncryptionBytesImpl;

        public EnvelopeEncryptionBytesImplTest()
        {
            envelopeEncryptionJsonImplMock = new Mock<EnvelopeEncryptionJsonImpl>();
            envelopeEncryptionBytesImpl = new EnvelopeEncryptionBytesImpl(envelopeEncryptionJsonImplMock.Object);
        }

        [Fact]
        private void TestDecryptDataRowRecord()
        {
            byte[] expectedBytes = { 0, 1 };

            envelopeEncryptionJsonImplMock.Setup(x => x.DecryptDataRowRecord(It.IsAny<JObject>()))
                .Returns(expectedBytes);

            ImmutableDictionary<string, string> immutableDictionary = new Dictionary<string, string> { { "key", "value" } }.ToImmutableDictionary();
            byte[] dataRowRecordBytes = new Asherah.AppEncryption.Util.Json(JObject.FromObject(immutableDictionary)).ToUtf8();
            byte[] actualBytes = envelopeEncryptionBytesImpl.DecryptDataRowRecord(dataRowRecordBytes);
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        private void TestEncryptPayload()
        {
            ImmutableDictionary<string, string> immutableDictionary = new Dictionary<string, string> { { "key", "value" } }.ToImmutableDictionary();
            JObject dataRowRecord = JObject.FromObject(immutableDictionary);
            byte[] expectedBytes = { 123, 34, 107, 101, 121, 34, 58, 34, 118, 97, 108, 117, 101, 34, 125 };

            envelopeEncryptionJsonImplMock.Setup(x => x.EncryptPayload(It.IsAny<byte[]>())).Returns(dataRowRecord);

            byte[] actualResult = envelopeEncryptionBytesImpl.EncryptPayload(new byte[] { 0, 1 });
            Assert.Equal(expectedBytes, actualResult);
        }

        [Fact]
        private void TestDisposeSuccess()
        {
            envelopeEncryptionBytesImpl.Dispose();

            // Verify proper resources are closed
            envelopeEncryptionJsonImplMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestDisposeWithDisposeFailShouldReturn()
        {
            envelopeEncryptionJsonImplMock.Setup(x => x.Dispose()).Throws(new SystemException());
            envelopeEncryptionBytesImpl.Dispose();

            envelopeEncryptionJsonImplMock.Verify(x => x.Dispose());
        }
    }
}

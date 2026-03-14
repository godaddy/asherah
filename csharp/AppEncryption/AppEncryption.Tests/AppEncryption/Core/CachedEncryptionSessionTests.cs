using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Core
{
    public class CachedEncryptionSessionTests
    {
        [Fact]
        public void Dispose_DoesNotDisposeUnderlyingEncryptionSession()
        {
            var envelopeMock = new Mock<IEnvelopeEncryption<byte[]>>();
            var encryptionSession = new EncryptionSession(envelopeMock.Object);
            var cached = new CachedEncryptionSession(encryptionSession);

            cached.Dispose();

            envelopeMock.Verify(e => e.Dispose(), Times.Never);
        }

        [Fact]
        public void DisposeUnderlying_DisposesUnderlyingEncryptionSession()
        {
            var envelopeMock = new Mock<IEnvelopeEncryption<byte[]>>();
            var encryptionSession = new EncryptionSession(envelopeMock.Object);
            var cached = new CachedEncryptionSession(encryptionSession);

            cached.DisposeUnderlying();

            envelopeMock.Verify(e => e.Dispose(), Times.Once);
        }
    }
}

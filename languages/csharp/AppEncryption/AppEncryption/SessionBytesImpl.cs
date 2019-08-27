using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption
{
    public class SessionBytesImpl<TD> : Session<byte[], TD>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionBytesImpl<TD>>();

        private readonly IEnvelopeEncryption<TD> envelopeEncryption;

        public SessionBytesImpl(IEnvelopeEncryption<TD> envelopeEncryption)
        {
            this.envelopeEncryption = envelopeEncryption;
        }

        public override byte[] Decrypt(TD dataRowRecord)
        {
            return envelopeEncryption.DecryptDataRowRecord(dataRowRecord);
        }

        public override TD Encrypt(byte[] payload)
        {
            return envelopeEncryption.EncryptPayload(payload);
        }

        public override void Dispose()
        {
            try
            {
                envelopeEncryption.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "unexpected exception during close");
            }
        }
    }
}

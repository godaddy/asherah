using System;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    public class EnvelopeEncryptionBytesImpl : IEnvelopeEncryption<byte[]>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<EnvelopeEncryptionBytesImpl>();

        private readonly EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;

        public EnvelopeEncryptionBytesImpl(EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl)
        {
            this.envelopeEncryptionJsonImpl = envelopeEncryptionJsonImpl;
        }

        public void Dispose()
        {
            try
            {
                envelopeEncryptionJsonImpl.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected exception during dispose");
            }
        }

        public virtual byte[] DecryptDataRowRecord(byte[] dataRowRecord)
        {
            Json dataRowRecordJson = new Json(dataRowRecord);
            return envelopeEncryptionJsonImpl.DecryptDataRowRecord(dataRowRecordJson.ToJObject());
        }

        public virtual byte[] EncryptPayload(byte[] payload)
        {
            Json drrJson = new Json(envelopeEncryptionJsonImpl.EncryptPayload(payload));
            return drrJson.ToUtf8();
        }
    }
}

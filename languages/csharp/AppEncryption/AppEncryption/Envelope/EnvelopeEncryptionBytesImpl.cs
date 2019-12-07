using System;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    public class EnvelopeEncryptionBytesImpl : IEnvelopeEncryption<byte[]>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<EnvelopeEncryptionBytesImpl>();

        private readonly IEnvelopeEncryption<JObject> envelopeEncryptionJson;

        public EnvelopeEncryptionBytesImpl(IEnvelopeEncryption<JObject> envelopeEncryptionJson)
        {
            this.envelopeEncryptionJson = envelopeEncryptionJson;
        }

        public void Dispose()
        {
            try
            {
                envelopeEncryptionJson.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected exception during dispose");
            }
        }

        public virtual byte[] DecryptDataRowRecord(byte[] dataRowRecord)
        {
            Json dataRowRecordJson = new Json(dataRowRecord);
            return envelopeEncryptionJson.DecryptDataRowRecord(dataRowRecordJson.ToJObject());
        }

        public virtual byte[] EncryptPayload(byte[] payload)
        {
            Json drrJson = new Json(envelopeEncryptionJson.EncryptPayload(payload));
            return drrJson.ToUtf8();
        }
    }
}

using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption
{
    public class AppEncryptionJsonImpl<TD> : AppEncryption<JObject, TD>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<AppEncryptionJsonImpl<TD>>();

        private readonly IEnvelopeEncryption<TD> envelopeEncryption;

        public AppEncryptionJsonImpl(IEnvelopeEncryption<TD> envelopeEncryption)
        {
            this.envelopeEncryption = envelopeEncryption;
        }

        public override JObject Decrypt(TD dataRowRecord)
        {
            byte[] jsonAsUtf8Bytes = envelopeEncryption.DecryptDataRowRecord(dataRowRecord);
            return new Json(jsonAsUtf8Bytes).ToJObject();
        }

        public override TD Encrypt(JObject payload)
        {
            byte[] jsonAsUtf8Bytes = new Json(payload).ToUtf8();
            return envelopeEncryption.EncryptPayload(jsonAsUtf8Bytes);
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

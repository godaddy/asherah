using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption
{
    /// <inheritdoc />
    public class SessionJsonImpl<TD> : Session<JObject, TD>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionJsonImpl<TD>>();

        private readonly IEnvelopeEncryption<TD> envelopeEncryption;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionJsonImpl{TD}"/> class using the provided
        /// <see cref="IEnvelopeEncryption{TD}"/> object. An implementation of <see cref="Session{TP,TD}"/> that
        /// encrypts a payload of type JObject.
        /// </summary>
        ///
        /// <param name="envelopeEncryption">An implementation of <see cref="IEnvelopeEncryption{TD}"/> that uses
        /// JObject as the Data Row Record format.</param>
        public SessionJsonImpl(IEnvelopeEncryption<TD> envelopeEncryption)
        {
            this.envelopeEncryption = envelopeEncryption;
        }

        /// <inheritdoc/>
        public override JObject Decrypt(TD dataRowRecord)
        {
            byte[] jsonAsUtf8Bytes = envelopeEncryption.DecryptDataRowRecord(dataRowRecord);
            return new Json(jsonAsUtf8Bytes).ToJObject();
        }

        /// <inheritdoc/>
        public override TD Encrypt(JObject payload)
        {
            byte[] jsonAsUtf8Bytes = new Json(payload).ToUtf8();
            return envelopeEncryption.EncryptPayload(jsonAsUtf8Bytes);
        }

        /// <inheritdoc/>
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

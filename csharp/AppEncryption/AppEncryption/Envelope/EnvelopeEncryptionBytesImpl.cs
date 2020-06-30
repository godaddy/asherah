using System;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <inheritdoc />
    public class EnvelopeEncryptionBytesImpl : IEnvelopeEncryption<byte[]>
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<EnvelopeEncryptionBytesImpl>();

        private readonly IEnvelopeEncryption<JObject> envelopeEncryptionJson;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeEncryptionBytesImpl"/> class using the provided
        /// parameters. This is an implementation of <see cref="IEnvelopeEncryption{TD}"/> which uses byte[] as the Data
        /// Row Record format.
        /// </summary>
        ///
        /// <param name="envelopeEncryptionJson">An <see cref="IEnvelopeEncryption{TD}"/> object which uses
        /// <see cref="JObject"/> as Data Row Record format.</param>
        public EnvelopeEncryptionBytesImpl(IEnvelopeEncryption<JObject> envelopeEncryptionJson)
        {
            this.envelopeEncryptionJson = envelopeEncryptionJson;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public virtual byte[] DecryptDataRowRecord(byte[] dataRowRecord)
        {
            Json dataRowRecordJson = new Json(dataRowRecord);
            return envelopeEncryptionJson.DecryptDataRowRecord(dataRowRecordJson.ToJObject());
        }

        /// <inheritdoc/>
        public virtual byte[] EncryptPayload(byte[] payload)
        {
            Json drrJson = new Json(envelopeEncryptionJson.EncryptPayload(payload));
            return drrJson.ToUtf8();
        }
    }
}

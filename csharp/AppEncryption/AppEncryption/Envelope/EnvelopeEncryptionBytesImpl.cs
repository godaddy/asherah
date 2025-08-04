using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.AppEncryption.Util;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "This class does not have a finalizer and does not need to suppress finalization.")]

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <inheritdoc />
    public class EnvelopeEncryptionBytesImpl : IEnvelopeEncryption<byte[]>
    {
        private readonly ILogger _logger;
        private readonly IEnvelopeEncryption<JObject> envelopeEncryptionJson;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeEncryptionBytesImpl"/> class using the provided
        /// parameters and logger. This is an implementation of <see cref="IEnvelopeEncryption{TD}"/> which uses byte[] as the Data
        /// Row Record format.
        /// </summary>
        ///
        /// <param name="envelopeEncryptionJson">An <see cref="IEnvelopeEncryption{TD}"/> object which uses
        /// <see cref="JObject"/> as Data Row Record format.</param>
        /// <param name="logger">The logger implementation to use.</param>
        public EnvelopeEncryptionBytesImpl(IEnvelopeEncryption<JObject> envelopeEncryptionJson, ILogger logger)
        {
            this.envelopeEncryptionJson = envelopeEncryptionJson;
            this._logger = logger;
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
                _logger?.LogError(e, "Unexpected exception during dispose");
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

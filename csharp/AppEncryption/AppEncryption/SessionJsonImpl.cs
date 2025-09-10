using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Util;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "This class does not have a finalizer and does not need to suppress finalization.")]

namespace GoDaddy.Asherah.AppEncryption
{
    /// <inheritdoc />
    public class SessionJsonImpl<TD> : Session<JObject, TD>
    {
        private readonly ILogger _logger;
        private readonly IEnvelopeEncryption<TD> envelopeEncryption;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionJsonImpl{TD}"/> class using the provided
        /// <see cref="IEnvelopeEncryption{TD}"/> object and logger. An implementation of <see cref="Session{TP,TD}"/> that
        /// encrypts a payload of type JObject.
        /// </summary>
        ///
        /// <param name="envelopeEncryption">An implementation of <see cref="IEnvelopeEncryption{TD}"/> that uses
        /// JObject as the Data Row Record format.</param>
        /// <param name="logger">The logger implementation to use.</param>
        public SessionJsonImpl(IEnvelopeEncryption<TD> envelopeEncryption, ILogger logger)
        {
            this.envelopeEncryption = envelopeEncryption;
            this._logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionJsonImpl{TD}"/> class using the provided
        /// <see cref="IEnvelopeEncryption{TD}"/> object. An implementation of <see cref="Session{TP,TD}"/> that
        /// encrypts a payload of type JObject. This constructor is provided for backwards compatibility and does not include logging.
        /// </summary>
        ///
        /// <param name="envelopeEncryption">An implementation of <see cref="IEnvelopeEncryption{TD}"/> that uses
        /// JObject as the Data Row Record format.</param>
        public SessionJsonImpl(IEnvelopeEncryption<TD> envelopeEncryption)
            : this(envelopeEncryption, null)
        {
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
        public override async Task<JObject> DecryptAsync(TD dataRowRecord)
        {
            return await Task.FromResult(Decrypt(dataRowRecord));
        }

        /// <inheritdoc/>
        public override async Task<TD> EncryptAsync(JObject payload)
        {
            return await Task.FromResult(Encrypt(payload));
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
                _logger?.LogError(e, "unexpected exception during close");
            }
        }
    }
}

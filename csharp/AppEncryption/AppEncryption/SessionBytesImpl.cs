using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Envelope;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "This class does not have a finalizer and does not need to suppress finalization.")]

namespace GoDaddy.Asherah.AppEncryption
{
    /// <inheritdoc />
    public class SessionBytesImpl<TD> : Session<byte[], TD>
    {
        private readonly ILogger _logger;
        private readonly IEnvelopeEncryption<TD> envelopeEncryption;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionBytesImpl{TD}"/> class using the provided
        /// <see cref="IEnvelopeEncryption{TD}"/> and logger. An implementation of <see cref="Session{TP,TD}"/> that encrypts a
        /// payload of type byte[].
        /// </summary>
        ///
        /// <param name="envelopeEncryption">An implementation of <see cref="envelopeEncryption"/> that uses byte[] as
        /// the Data Row Record format.</param>
        /// <param name="logger">The logger implementation to use.</param>
        public SessionBytesImpl(IEnvelopeEncryption<TD> envelopeEncryption, ILogger logger)
        {
            this.envelopeEncryption = envelopeEncryption;
            this._logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionBytesImpl{TD}"/> class using the provided
        /// <see cref="IEnvelopeEncryption{TD}"/>. An implementation of <see cref="Session{TP,TD}"/> that encrypts a
        /// payload of type byte[]. This constructor is provided for backwards compatibility and does not include logging.
        /// </summary>
        ///
        /// <param name="envelopeEncryption">An implementation of <see cref="envelopeEncryption"/> that uses byte[] as
        /// the Data Row Record format.</param>
        public SessionBytesImpl(IEnvelopeEncryption<TD> envelopeEncryption)
            : this(envelopeEncryption, null)
        {
        }

        /// <inheritdoc/>
        public override byte[] Decrypt(TD dataRowRecord)
        {
            return envelopeEncryption.DecryptDataRowRecord(dataRowRecord);
        }

        /// <inheritdoc/>
        public override TD Encrypt(byte[] payload)
        {
            return envelopeEncryption.EncryptPayload(payload);
        }

        /// <inheritdoc/>
        public override async Task<byte[]> DecryptAsync(TD dataRowRecord)
        {
            return await Task.FromResult(Decrypt(dataRowRecord));
        }

        /// <inheritdoc/>
        public override async Task<TD> EncryptAsync(byte[] payload)
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

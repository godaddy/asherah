using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Envelope;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Provides encryption and decryption operations for a specific partition.
    /// </summary>
    internal class EncryptionSession : IEncryptionSession
    {
        private readonly IEnvelopeEncryption<byte[]> _envelopeEncryption;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionSession"/> class.
        /// </summary>
        /// <param name="envelopeEncryption">The envelope encryption implementation to delegate to.</param>
        internal EncryptionSession(IEnvelopeEncryption<byte[]> envelopeEncryption)
        {
            _envelopeEncryption = envelopeEncryption;
        }

        /// <summary>
        /// Encrypts a payload and returns the encrypted data row record.
        /// </summary>
        /// <param name="payload">The payload to encrypt.</param>
        /// <returns>The encrypted data row record.</returns>
        public byte[] Encrypt(byte[] payload)
        {
            return _envelopeEncryption.EncryptPayload(payload);
        }

        /// <summary>
        /// Encrypts a payload and returns the encrypted data row record asynchronously.
        /// </summary>
        /// <param name="payload">The payload to encrypt.</param>
        /// <returns>The encrypted data row record.</returns>
        public Task<byte[]> EncryptAsync(byte[] payload)
        {
            return _envelopeEncryption.EncryptPayloadAsync(payload);
        }

        /// <summary>
        /// Decrypts a data row record and returns the original payload.
        /// </summary>
        /// <param name="dataRowRecord">The encrypted data row record.</param>
        /// <returns>The decrypted payload.</returns>
        public byte[] Decrypt(byte[] dataRowRecord)
        {
            return _envelopeEncryption.DecryptDataRowRecord(dataRowRecord);
        }

        /// <summary>
        /// Decrypts a data row record and returns the original payload asynchronously.
        /// </summary>
        /// <param name="dataRowRecord">The encrypted data row record.</param>
        /// <returns>The decrypted payload.</returns>
        public Task<byte[]> DecryptAsync(byte[] dataRowRecord)
        {
            return _envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecord);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _envelopeEncryption.Dispose();
        }
    }
}

using System;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// This defines the interface for interacting with the envelope encryption algorithm. It only interacts with bytes,
    /// so it is up to the caller to determine how the bytes map to the first class object being used
    /// (e.g. JSON, String, etc.).
    /// </summary>
    /// <typeparam name="TD">The type that is being used as the Data Row Record format (e.g. JSON, Yaml, Protobuf, etc.).
    /// </typeparam>
    public interface IEnvelopeEncryption<TD> : IDisposable
    {
        /// <summary>
        /// Uses an envelope encryption algorithm to decrypt a Data Row Record and return the payload.
        /// </summary>
        /// <param name="dataRowRecord">Value to decrypt</param>
        /// <returns>A decrypted payload as bytes.</returns>
        byte[] DecryptDataRowRecord(TD dataRowRecord);

        /// <summary>
        /// Uses an envelope encryption algorithm to encrypt a payload and return the resulting Data Row Record.
        /// </summary>
        /// <param name="payload">Payload to encrypt</param>
        /// <returns>The Data Row Record that contains the now-encrypted payload and corresponding Data Row Key.</returns>
        TD EncryptPayload(byte[] payload);
    }
}

using System;
using System.Threading.Tasks;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Provides encryption and decryption operations for a specific partition.
    /// </summary>
    public interface IEncryptionSession : IDisposable
    {
        /// <summary>
        /// Encrypts a payload and returns the encrypted data row record.
        /// </summary>
        /// <param name="payload">The payload to encrypt.</param>
        /// <returns>The encrypted data row record.</returns>
        byte[] Encrypt(byte[] payload);

        /// <summary>
        /// Encrypts a payload and returns the encrypted data row record asynchronously.
        /// </summary>
        /// <param name="payload">The payload to encrypt.</param>
        /// <returns>The encrypted data row record.</returns>
        Task<byte[]> EncryptAsync(byte[] payload);

        /// <summary>
        /// Decrypts a data row record and returns the original payload.
        /// </summary>
        /// <param name="dataRowRecord">The encrypted data row record.</param>
        /// <returns>The decrypted payload.</returns>
        byte[] Decrypt(byte[] dataRowRecord);

        /// <summary>
        /// Decrypts a data row record and returns the original payload asynchronously.
        /// </summary>
        /// <param name="dataRowRecord">The encrypted data row record.</param>
        /// <returns>The decrypted payload.</returns>
        Task<byte[]> DecryptAsync(byte[] dataRowRecord);
    }
}

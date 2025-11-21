using System;
using System.Threading.Tasks;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    /// <summary>
    /// A key management service is a pluggable component used by Asherah which generates the top level master key,
    /// which in turn is used to encrypt the system keys. It enables the user to use a HSM for providing the master key
    /// or staying cloud agnostic if using a hosted key management service.
    /// </summary>
    public interface IKeyManagementService
    {
        /// <summary>
        /// Encrypts a <see cref="CryptoKey"/> using the implemented key management service.
        /// </summary>
        ///
        /// <param name="key">The key to encrypt.</param>
        /// <returns>The encrypted key in form of a byte[].</returns>
        byte[] EncryptKey(CryptoKey key);

        /// <summary>
        /// Takes the encrypted key as the parameter and decrypts the key using the implemented key management service.
        /// </summary>
        ///
        /// <param name="keyCipherText">The encrypted key.</param>
        /// <param name="keyCreated">Creation time of the key.</param>
        /// <param name="revoked">Revoked status of the key. True if revoked, else False.</param>
        /// <returns>The decrypted key.</returns>
        CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked);

        /// <summary>
        /// Encrypts a <see cref="CryptoKey"/> using the implemented key management service.
        /// </summary>
        ///
        /// <param name="key">The key to encrypt.</param>
        /// <returns>The encrypted key in form of a byte[].</returns>
        Task<byte[]> EncryptKeyAsync(CryptoKey key);

        /// <summary>
        /// Takes the encrypted key as the parameter and decrypts the key using the implemented key management service.
        /// </summary>
        ///
        /// <param name="keyCipherText">The encrypted key.</param>
        /// <param name="keyCreated">Creation time of the key.</param>
        /// <param name="revoked">Revoked status of the key. True if revoked, else False.</param>
        /// <returns>The decrypted key.</returns>
        Task<CryptoKey> DecryptKeyAsync(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked);
    }
}

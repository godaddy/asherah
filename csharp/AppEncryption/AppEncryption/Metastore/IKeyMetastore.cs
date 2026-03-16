using System;
using System.Threading.Tasks;

namespace GoDaddy.Asherah.AppEncryption.Metastore
{
    /// <summary>
    /// The KeyMetastore interface provides methods that can be used to load and store system and intermediate keys from a
    /// supported database using key records.
    /// </summary>
    public interface IKeyMetastore
    {
        /// <summary>
        /// Attempts to load the key record associated with the keyId and created time.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <param name="created">The created time to lookup.</param>
        /// <returns>A tuple containing a boolean indicating if the key record was found and the key record if found.</returns>
        Task<(bool found, IKeyRecord keyRecord)> TryLoadAsync(string keyId, DateTimeOffset created);

        /// <summary>
        /// Attempts to load the latest key record associated with the keyId.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <returns>A tuple containing a boolean indicating if a key record was found and the latest key record if found.</returns>
        Task<(bool found, IKeyRecord keyRecord)> TryLoadLatestAsync(string keyId);

        /// <summary>
        /// Stores the key record using the specified keyId and created time.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to store.</param>
        /// <param name="created">The created time to store.</param>
        /// <param name="keyRecord">The key record to store.</param>
        /// <returns>True if the store succeeded, false if the store failed for a known condition e.g., trying to save
        /// a duplicate value should return false, not throw an exception.</returns>
        Task<bool> StoreAsync(string keyId, DateTimeOffset created, IKeyRecord keyRecord);

        /// <summary>
        /// Returns the key suffix or "" if key suffix option is disabled.
        /// </summary>
        ///
        /// <returns>
        /// The key suffix.
        /// </returns>
        string GetKeySuffix();
    }
}

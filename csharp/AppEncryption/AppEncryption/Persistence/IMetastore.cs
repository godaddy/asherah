using System;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    public interface IMetastore<T>
    {
        /// <summary>
        /// Lookup the keyId and created time and return its associated value, if any.
        /// </summary>
        ///
        /// <returns>
        /// The value associated with the keyId and created tuple, if any.
        /// </returns>
        ///
        /// <param name="keyId">the keyId to lookup</param>
        /// <param name="created">the created time to lookup</param>
        Option<T> Load(string keyId, DateTimeOffset created);

        /// <summary>
        /// Lookup the latest value associated with the keyId.
        /// </summary>
        ///
        /// <returns>
        /// The latest value associated with the keyId, if any.
        /// </returns>
        ///
        /// <param name="keyId">the keyId to lookup</param>
        Option<T> LoadLatest(string keyId);

        /// <summary>
        /// Stores the value using the specified keyId and created time.
        /// </summary>
        ///
        /// <returns>
        /// true if the store succeeded, false is the store failed for a known condition
        /// e.g., trying to save a duplicate value should return false, not throw an exception.
        /// </returns>
        ///
        /// <param name="keyId">the keyId to store</param>
        /// <param name="created">the created time to store</param>
        /// <param name="value">the value to store</param>
        bool Store(string keyId, DateTimeOffset created, T value);
    }
}

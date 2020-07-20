using System;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    /// <summary>
    /// The Metastore interface provides methods that can be used to load and store system and intermediate keys from a
    /// supported database.
    /// </summary>
    ///
    /// <typeparam name="T">The type of value to store and retrieve from the metastore.</typeparam>
    public interface IMetastore<T>
    {
        /// <summary>
        /// Lookup the keyId and created time and return its associated value, if any.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <param name="created">The created time to lookup.</param>
        /// <returns>The value associated with the keyId and created tuple, if any.</returns>
        Option<T> Load(string keyId, DateTimeOffset created);

        /// <summary>
        /// Lookup the latest value associated with the keyId.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <returns>The latest value associated with the keyId, if any.</returns>
        Option<T> LoadLatest(string keyId);

        /// <summary>
        /// Stores the value using the specified keyId and created time.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to store.</param>
        /// <param name="created">The created time to store.</param>
        /// <param name="value">The value to store.</param>
        /// <returns><value>True</value> if the store succeeded, false is the store failed for a known condition e.g., trying to save
        /// a duplicate value should return false, not throw an exception.</returns>
        bool Store(string keyId, DateTimeOffset created, T value);
    }
}

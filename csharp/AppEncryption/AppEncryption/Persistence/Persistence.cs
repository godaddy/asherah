using System;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    /// <summary>
    /// This is the abstract class used for loading and storing internal metadata and Data Row Records.
    /// </summary>
    ///
    /// <typeparam name="T">The type of the value being loaded and stored in the persistent store.</typeparam>
    public abstract class Persistence<T>
    {
        /// <summary>
        /// Lookup the key and return its associated value, if any.
        /// </summary>
        ///
        /// <returns>
        /// The value associated with the key, if any.
        /// </returns>
        ///
        /// <param name="key">the key to lookup</param>
        public abstract Option<T> Load(string key);

        /// <summary>
        /// Stores the value using the specified key.
        /// </summary>
        ///
        /// <param name="key">the key used to store the value</param>
        /// <param name="value">the value to be stored</param>
        public abstract void Store(string key, T value);

        /// <summary>
        /// Stores the value and returns its associated generated key for future lookup (e.g. UUID, etc.).
        /// </summary>
        ///
        /// <returns>
        /// The generated key that can be used for looking up this value.
        /// </returns>
        ///
        /// <param name="value">the value to store</param>
        public virtual string Store(T value)
        {
            string persistenceKey = GenerateKey(value);
            Store(persistenceKey, value);
            return persistenceKey;
        }

        /// <summary>
        /// Generates a persistence key, possibly based on value or type, for use with store calls with no predefined key. Defaults to
        /// a random UUID.
        /// </summary>
        ///
        /// <returns>
        /// The key that was generated.
        /// </returns>
        ///
        /// <param name="value">the value based on which persistence key should be generated</param>
        public virtual string GenerateKey(T value)
        {
            return Guid.NewGuid().ToString();
        }
    }
}

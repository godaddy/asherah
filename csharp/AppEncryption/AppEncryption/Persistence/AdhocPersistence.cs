using System;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    /// <summary>
    /// Convenient <see cref="Persistence{T}"/> implementation that allows functional interfaces to be passed in for
    /// load and store implementations.
    /// </summary>
    ///
    /// <typeparam name="T">The type of value being loaded and stored in the persistent store.</typeparam>
    public class AdhocPersistence<T> : Persistence<T>
    {
        private readonly Func<string, Option<T>> persistenceLoad;
        private readonly Action<string, T> persistenceStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdhocPersistence{T}"/> class.
        /// </summary>
        ///
        /// <param name="load">A user-defined <see cref="Func{TResult}"/> object that loads a result from the persistent
        /// store.
        /// </param>
        /// <param name="store">A user-defined <see cref="Action"/> object that stores a record to the persistent store.
        /// </param>
        public AdhocPersistence(Func<string, Option<T>> load, Action<string, T> store)
        {
            persistenceLoad = load;
            persistenceStore = store;
        }

        /// <inheritdoc/>
        public override Option<T> Load(string key)
        {
            return persistenceLoad(key);
        }

        /// <inheritdoc/>
        public override void Store(string key, T value)
        {
            persistenceStore(key, value);
        }
    }
}

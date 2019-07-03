using System;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    public class AdhocPersistence<T> : Persistence<T>
    {
        private readonly Func<string, Option<T>> persistenceLoad;
        private readonly Action<string, T> persistenceStore;

        public AdhocPersistence(Func<string, Option<T>> load, Action<string, T> store)
        {
            persistenceLoad = load;
            persistenceStore = store;
        }

        public override Option<T> Load(string key)
        {
            return persistenceLoad(key);
        }

        public override void Store(string key, T value)
        {
            persistenceStore(key, value);
        }
    }
}

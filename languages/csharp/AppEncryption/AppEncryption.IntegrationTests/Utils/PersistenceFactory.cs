using System.Collections.Concurrent;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class PersistenceFactory<T>
    {
        public static Persistence<T> CreateInMemoryPersistence()
        {
            return new DictionaryPersistence();
        }

        private class DictionaryPersistence : Persistence<T>
        {
            private readonly ConcurrentDictionary<string, T> dictionaryPersistence = new ConcurrentDictionary<string, T>();

            public override Option<T> Load(string key)
            {
                return dictionaryPersistence.TryGetValue(key, out T result) ? result : Option<T>.None;
            }

            public override void Store(string key, T value)
            {
                dictionaryPersistence[key] = value;
            }
        }
    }
}

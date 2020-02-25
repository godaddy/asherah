using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GoDaddy.Asherah.Crypto.Keys
{
    public class SecureCryptoKeyDictionary<TKey> : IDisposable
    {
        private readonly ConcurrentDictionary<TKey, SharedCryptoKeyEntry> sharedCryptoKeyDictionary =
            new ConcurrentDictionary<TKey, SharedCryptoKeyEntry>();

        private readonly long revokeCheckPeriodMillis;
        private volatile int isClosed = 0; // using volatile int as 0/1 value to mimic Java's AtomicBoolean functionality

        public SecureCryptoKeyDictionary(long revokeCheckPeriodMillis)
        {
            this.revokeCheckPeriodMillis = revokeCheckPeriodMillis;
        }

        public virtual CryptoKey Get(TKey key)
        {
            if (!Convert.ToBoolean(isClosed))
            {
                if (sharedCryptoKeyDictionary.TryGetValue(key, out SharedCryptoKeyEntry entry))
                {
                    // If the key is already revoked, we can just return it since it's the key's final state.
                    // Otherwise, for a non-revoked key, we return it if it's not yet time to recheck the metastore.
                    if (entry.SharedCryptoKey.IsRevoked()
                        || (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref entry.CachedTimeMillis)) <
                        revokeCheckPeriodMillis)
                    {
                        return entry.SharedCryptoKey;
                    }
                }

                return null;
            }

            throw new InvalidOperationException("Attempted to get CryptoKey after close");
        }

        public virtual CryptoKey GetLast()
        {
            if (!Convert.ToBoolean(isClosed))
            {
                if (!sharedCryptoKeyDictionary.IsEmpty)
                {
                    IOrderedEnumerable<KeyValuePair<TKey, SharedCryptoKeyEntry>> sortedSharedCryptoKeyDictionary =
                        sharedCryptoKeyDictionary.OrderBy(x => x.Key);
                    KeyValuePair<TKey, SharedCryptoKeyEntry> lastEntry = sortedSharedCryptoKeyDictionary.Last();

                    // If the key is already revoked, we can just return it since it's the key's final state.
                    // Otherwise, for a non-revoked key, we return it if it's not yet time to recheck the metastore.
                    if (lastEntry.Value.SharedCryptoKey.IsRevoked()
                        || (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref lastEntry.Value.CachedTimeMillis)) <
                        revokeCheckPeriodMillis)
                    {
                        return lastEntry.Value.SharedCryptoKey;
                    }
                }

                return null;
            }

            throw new InvalidOperationException("Attempted to get CryptoKey after close");
        }

        /// <summary>
        /// Put a CryptoKey into the cache and return a CryptoKey that should be used
        /// in place of the cryptoKey parameter that was provided.  This method manages
        /// the disposing of the return value such that it can (and should)
        /// always be disposed by the calling method.
        /// </summary>
        ///
        /// <returns>the CryptoKey which should be used and subsequently closed after use</returns>
        ///
        /// <example>
        /// <code>
        /// SecureCryptoKeyDictionary dictionary = new SecureCryptoKeyDictionary&lt;string&gt;();
        /// CryptoKey key = ...;
        ///
        /// // Cache the key and use what was returned
        /// key = map.PutAndGetUsable("key_string", key);
        ///
        /// PerformActionWithKey(key, ...);
        ///
        /// // Because we re-assigned key to what was returned from the putAndGetUsable() call, we
        /// // know we it's always ok (and the right thing to do) to close it.
        /// key.Dispose();
        /// </code>
        /// </example>
        ///
        /// <param name="key">the key to store the cryptoKey</param>
        /// <param name="cryptoKey">the cryptoKey to store</param>
        public virtual CryptoKey PutAndGetUsable(TKey key, CryptoKey cryptoKey)
        {
            if (!Convert.ToBoolean(isClosed))
            {
                bool addedToCache = sharedCryptoKeyDictionary.TryAdd(
                    key, new SharedCryptoKeyEntry(new SharedCryptoKey(cryptoKey), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

                // NOTE: We know cacheValue will always be non-null at this point
                sharedCryptoKeyDictionary.TryGetValue(key, out SharedCryptoKeyEntry cacheValue);
                if (addedToCache)
                {
                    // We want to return something that can always be closed by the calling method.
                    // If TryAdd returns true, the value was put into the cache, so return the new "shared" version whose
                    // Dispose() calls will not close the underlying now-cached Secret.
                    // ReSharper disable once PossibleNullReferenceException
                    return cacheValue.SharedCryptoKey;
                }

                // If false returned then it's already cached. If the passed in value is now revoked,
                // mark the shared/cached version as revoked. Otherwise, just update the cachedTime of the shared/cached value
                if (cryptoKey.IsRevoked())
                {
                    // ReSharper disable once PossibleNullReferenceException
                    cacheValue.SharedCryptoKey.MarkRevoked();
                }
                else
                {
                    // ReSharper disable once PossibleNullReferenceException
                    Interlocked.Exchange(ref cacheValue.CachedTimeMillis, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }

                // Return the passed in CryptoKey so caller can safely close it without affecting other threads
                return cryptoKey;
            }

            throw new InvalidOperationException("Attempted to get CryptoKey after close");
        }

        public virtual void Dispose()
        {
            // Will return 0 on the first and only successful compare and set
            if (!Convert.ToBoolean(Interlocked.CompareExchange(ref isClosed, 1, 0)))
            {
                foreach (SharedCryptoKeyEntry sharedCryptoKeyEntry in sharedCryptoKeyDictionary.Values)
                {
                    sharedCryptoKeyEntry.SharedCryptoKey.SharedKey.Dispose();
                }

                sharedCryptoKeyDictionary.Clear();
            }
        }

        private class SharedCryptoKeyEntry
        {
            #pragma warning disable SA1401
            internal long CachedTimeMillis;
            #pragma warning restore SA1401

            public SharedCryptoKeyEntry(SharedCryptoKey sharedCryptoKey, long cachedTimeMillis)
            {
                SharedCryptoKey = sharedCryptoKey;
                CachedTimeMillis = cachedTimeMillis;
            }

            internal SharedCryptoKey SharedCryptoKey { get; }
        }
    }
}

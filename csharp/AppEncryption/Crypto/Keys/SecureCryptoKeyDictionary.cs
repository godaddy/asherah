using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using GoDaddy.Asherah.SecureMemory;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "This class does not have a finalizer and does not need to suppress finalization.")]

namespace GoDaddy.Asherah.Crypto.Keys
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Public API - cannot change without breaking consumers")]
    public class SecureCryptoKeyDictionary<TKey> : IDisposable
    {
        private readonly ConcurrentDictionary<TKey, SharedCryptoKeyEntry> sharedCryptoKeyDictionary =
            new ConcurrentDictionary<TKey, SharedCryptoKeyEntry>();

        private readonly long revokeCheckPeriodMillis;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "Explicit initialization required for Interlocked operations")]
        private volatile int isClosed = 0; // using volatile int as 0/1 value to mimic Java's AtomicBoolean functionality

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureCryptoKeyDictionary{TKey}"/> class.
        /// </summary>
        ///
        /// <param name="revokeCheckPeriodMillis">The time, in milliseconds, after which the key revocation
        /// status should be checked.</param>
        public SecureCryptoKeyDictionary(long revokeCheckPeriodMillis)
        {
            this.revokeCheckPeriodMillis = revokeCheckPeriodMillis;
        }

        /// <summary>
        /// Retrieves the provided key from the cache.
        /// </summary>
        ///
        /// <param name="key">The key to retrieve.</param>
        /// <returns>A <see cref="CryptoKey"/> object.</returns>
        /// <exception cref="InvalidOperationException">If a closed key is accessed.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Public API - cannot change without breaking consumers")]
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

        /// <summary>
        /// Retrieves the last key from the cache.
        /// </summary>
        ///
        /// <returns>A <see cref="CryptoKey"/> object.</returns>
        /// <exception cref="InvalidOperationException">If a closed key is accessed.</exception>
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
                    return cacheValue.SharedCryptoKey;
                }

                // If false returned then it's already cached. If the passed in value is now revoked,
                // mark the shared/cached version as revoked. Otherwise, just update the cachedTime of the shared/cached value
                if (cryptoKey.IsRevoked())
                {
                    cacheValue.SharedCryptoKey.MarkRevoked();
                }
                else
                {
                    Interlocked.Exchange(ref cacheValue.CachedTimeMillis, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }

                // Return the passed in CryptoKey so caller can safely close it without affecting other threads
                return cryptoKey;
            }

            throw new InvalidOperationException("Attempted to get CryptoKey after close");
        }

        /// <inheritdoc />
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
            internal long CachedTimeMillis;

            public SharedCryptoKeyEntry(SharedCryptoKey sharedCryptoKey, long cachedTimeMillis)
            {
                SharedCryptoKey = sharedCryptoKey;
                CachedTimeMillis = cachedTimeMillis;
            }

            internal SharedCryptoKey SharedCryptoKey { get; }
        }
    }
}

package com.godaddy.asherah.crypto.keys;

import java.util.Map;
import java.util.concurrent.ConcurrentSkipListMap;
import java.util.concurrent.atomic.AtomicBoolean;

import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;

public class SecureCryptoKeyMap<K> implements SafeAutoCloseable {
  private final ConcurrentSkipListMap<K, SharedCryptoKeyEntry> sharedCryptoKeyMap = new ConcurrentSkipListMap<>();
  private final AtomicBoolean isClosed = new AtomicBoolean(false);
  private final long revokeCheckPeriodMillis;

  /**
   * Constructor for SecureCryptoKeyMap.
   * @param revokeCheckPeriodMillis The time, in milliseconds, after which the key revocation status should be checked.
   */
  public SecureCryptoKeyMap(final long revokeCheckPeriodMillis) {
    this.revokeCheckPeriodMillis = revokeCheckPeriodMillis;
  }

  /**
   * Retrieves the provided key from the cache.
   * @param key The key to retrieve.
   * @return a {@link CryptoKey} object.
   * @throws IllegalStateException if a closed key is accessed.
   */
  public CryptoKey get(final K key) {
    if (!isClosed.get()) {
      SharedCryptoKeyEntry entry = sharedCryptoKeyMap.get(key);
      if (entry != null) {
        // If the key is already revoked, we can just return it since it's the key's final state.
        // Otherwise, for a non-revoked key, we return it if it's not yet time to recheck the metastore.
        if (entry.sharedCryptoKey.isRevoked()
            || (System.currentTimeMillis() - entry.cachedTimeMillis) < revokeCheckPeriodMillis) {
          return entry.sharedCryptoKey;
        }
      }

      return null;
    }
    else {
      throw new IllegalStateException("Attempted to get CryptoKey after close");
    }
  }

  /**
   * Retrieves the last key from the cache.
   * @return a {@link CryptoKey} object.
   * @throws IllegalStateException if a closed key is accessed
   */
  public CryptoKey getLast() {
    if (!isClosed.get()) {
      Map.Entry<K, SharedCryptoKeyEntry> lastEntry = sharedCryptoKeyMap.lastEntry();
      if (lastEntry != null) {
        // If the key is already revoked, we can just return it since it's the key's final state.
        // Otherwise, for a non-revoked key, we return it if it's not yet time to recheck the metastore.
        if (lastEntry.getValue().sharedCryptoKey.isRevoked()
            || (System.currentTimeMillis() - lastEntry.getValue().cachedTimeMillis) < revokeCheckPeriodMillis) {
          return lastEntry.getValue().sharedCryptoKey;
        }
      }

      return null;
    }
    else {
      throw new IllegalStateException("Attempted to get CryptoKey after close");
    }
  }

  /**
   * Put a CryptoKey into the cache and return a CryptoKey that should be used
   * in place of the cryptoKey parameter that was provided.  This method manages
   * the closeability of the return value such that it can (and should)
   * always be closed by the calling method.
   *
   * Example:
   * <pre>{@code
   * SecureCryptoKeyMap map = new SecureCryptoKeyMap<String>();
   * CryptoKey key = ...;
   *
   * // Cache the key and use what was returned
   * key = map.putAndGetUsable("key_string", key);
   *
   * performActionWithKey(key, ...);
   *
   * // Because we re-assigned key to what was returned from the putAndGetUsable() call, we
   * // know we it's always ok (and the right thing to do) to close it.
   * key.close();
   * }</pre>
   *
   * @param key the key to store the cryptoKey.
   * @param cryptoKey the cryptoKey to store.
   * @return the CryptoKey which should be used and subsequently closed after use.
   */
  public CryptoKey putAndGetUsable(final K key, final CryptoKey cryptoKey) {
    if (!isClosed.get()) {
      SharedCryptoKeyEntry cacheValue = sharedCryptoKeyMap.putIfAbsent(key,
          new SharedCryptoKeyEntry(new SharedCryptoKey(cryptoKey), System.currentTimeMillis()));

      // We want to return something that can always be closed by the calling method.
      // If putIfAbsent returns null, the value was put into the cache, so return the new "shared" version whose
      // close() calls will not close the underlying now-cached Secret.
      if (cacheValue == null) {
        return sharedCryptoKeyMap.get(key).sharedCryptoKey;
      }
      else {
        // If non-null value returned then it's already cached. If the passed in value is now revoked,
        // mark the shared/cached version as revoked. Otherwise, just update the cachedTime of the shared/cached value
        if (cryptoKey.isRevoked()) {
          cacheValue.sharedCryptoKey.markRevoked();
        }
        else {
          cacheValue.cachedTimeMillis = System.currentTimeMillis();
        }

        // Return the passed in CryptoKey so caller can safely close it without affecting other threads
        return cryptoKey;
      }
    }
    else {
      throw new IllegalStateException("Attempted to store CryptoKey after close");
    }
  }

  @Override
  public void close() {
    // Close all the keys and clear the underlying map
    if (isClosed.compareAndSet(false, true)) {
      for (SharedCryptoKeyEntry sharedCryptoKeyEntry : sharedCryptoKeyMap.values()) {
        sharedCryptoKeyEntry.sharedCryptoKey.getSharedKey().close();
      }

      sharedCryptoKeyMap.clear();
    }

    // else already closed/closing
  }

  private static class SharedCryptoKeyEntry {
    private final SharedCryptoKey sharedCryptoKey;
    private volatile long cachedTimeMillis;

    SharedCryptoKeyEntry(final SharedCryptoKey sharedCryptoKey, final long cachedTimeMillis) {
      this.sharedCryptoKey = sharedCryptoKey;
      this.cachedTimeMillis = cachedTimeMillis;
    }
  }

}

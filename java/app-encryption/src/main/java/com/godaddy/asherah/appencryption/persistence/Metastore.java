package com.godaddy.asherah.appencryption.persistence;

import java.time.Instant;
import java.util.Optional;

/**
 * The {@code Metastore} interface provides methods that can be
 * used to load and store system and intermediate keys from a
 * supported database.
 *
 * @param <V> The type of the value being loaded and stored in the metastore.
 */
public interface Metastore<V> {

  /**
   * Lookup the keyId and created time and return its associated value, if any.
   *
   * @param keyId The keyId part of the lookup key.
   * @param created The created time part of the lookup key.
   * @return The value associated with the keyId and created tuple, if any.
   */
  Optional<V> load(String keyId, Instant created);

  /**
   * Lookup the latest value associated with the keyId.
   *
   * @param keyId The keyId part of the lookup key.
   * @return The latest value associated with the keyId, if any.
   */
  Optional<V> loadLatest(String keyId);

  /**
   * Stores the value using the specified keyId and created time.
   *
   * @param keyId The keyId part of the lookup key.
   * @param created The created time part of the lookup key.
   * @param value The value to store.
   * @return {@code true} if the store succeeded, false if the store failed for a known condition
   *         e.g., trying to save a duplicate value should return false, not throw an exception.
   */
  boolean store(String keyId, Instant created, V value);
}

package com.godaddy.asherah.appencryption.persistence;

import java.util.Optional;
import java.util.UUID;

/**
 * This is the interface used for loading and storing internal metadata and Data Row Records.
 *
 * @param <T> The type of the value being loaded and stored in the persistent store.
 */
public interface Persistence<T> {
  /**
   * Lookup the key and return its associated value, if any.
   *
   * @param key The key to lookup.
   * @return The value associated with the key, if any.
   */
  Optional<T> load(String key);

  /**
   * Stores the value using the specified key.
   *
   * @param key The key to associate with.
   * @param value The value to store.
   */
  void store(String key, T value);

  /**
   * Stores the value and returns its associated generated key for future lookup (e.g. UUID, etc.).
   *
   * @param value The value to store.
   * @return The generated key that can be used for looking up this value.
   */
  default String store(final T value) {
    String persistenceKey = generateKey(value);
    store(persistenceKey, value);
    return persistenceKey;
  }

  /**
   * Generates a persistence key, possibly based on value or type, for use with store calls with no predefined key.
   * Defaults to a random UUID.
   *
   * @param value The value which the generated key may be based off.
   * @return The key that was generated.
   */
  default String generateKey(T value) {
    return UUID.randomUUID().toString();
  }
}

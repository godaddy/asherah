package com.godaddy.asherah.appencryption.persistence;

import java.time.Instant;
import java.util.Optional;

public interface Metastore<V> {

  /**
   * Lookup the keyId and created time and return its associated value, if any.
   * @param keyId the keyId part of the lookup key
   * @param created the created time part of the lookup key
   * @return The value associated with the keyId and created tuple, if any.
   */
  Optional<V> load(String keyId, Instant created);

  /**
   * Lookup the latest value associated with the keyId.
   * @param keyId the keyId part of the lookup key
   * @return The latest value associated with the keyId, if any.
   */
  Optional<V> loadLatest(String keyId);

  /**
   * Stores the value using the specified keyId and created time.
   * @param keyId the keyId part of the lookup key
   * @param created the created time part of the lookup key
   * @param value the value to store
   * @return true if the store succeeded, false if the store failed for a known condition
   *         e.g., trying to save a duplicate value should return false, not throw an exception.
   */
  boolean store(String keyId, Instant created, V value);

  /**
   * Returns the region suffix for the metastore or "" if region suffix option is disabled
   * @return The region suffix
   */
  String getRegionSuffix();
}

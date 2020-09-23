package com.godaddy.asherah.crypto;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

public interface CryptoPolicy {
  enum KeyRotationStrategy {
    INLINE,
    QUEUED
  }

  /**
   * Checks if the key is expired.
   * @param keyCreationDate The key creation date as an {@link java.time.Instant} object.
   * @return {@code true} if the key is expired, else {@code false}.
   */
  boolean isKeyExpired(Instant keyCreationDate);

  /**
   * Gets the key revoke check time in milliseconds.
   * @return The key revoke check time in milliseconds.
   */
  long getRevokeCheckPeriodMillis();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of SystemKeys.
   * @return {@code true} if SystemKey caching is enabled, else {@code false}.
   */
  boolean canCacheSystemKeys();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of IntermediateKeys.
   * @return {@code true} if IntermediateKey caching is enabled, else {@code false}.
   */
  boolean canCacheIntermediateKeys();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of {@link com.godaddy.asherah.appencryption.Session}.
   * @return {@code true} if {@link com.godaddy.asherah.appencryption.Session} caching is enabled,
   *          else {@code false}.
   */
  boolean canCacheSessions();

  /**
   * Gets the maximum number of {@link com.godaddy.asherah.appencryption.Session} objects that can be cached.
   * @return The size of the session cache.
   */
  long getSessionCacheMaxSize();

  /**
   * Gets the session cache expiry time limit in milliseconds.
   * @return The session cache expiry time limit in milliseconds.
   */
  long getSessionCacheExpireMillis();

  /**
   * Checks if a notification should be sent when a DRK is using an expired IK.
   * @return {@code true} if notification sending is enabled, else {@code false}.
   */
  boolean notifyExpiredIntermediateKeyOnRead();

  /**
   * Checks if a notification should be sent when an expired SK is used during read.
   * @return {@code true} if notification sending is enabled, else {@code false}.
   */
  boolean notifyExpiredSystemKeyOnRead();

  /**
   * Get the key rotation strategy.
   * @return A {@link CryptoPolicy.KeyRotationStrategy} object.
   */
  KeyRotationStrategy keyRotationStrategy();

  /**
   * Checks if the key rotation strategy is {@link KeyRotationStrategy#INLINE}.
   * @return {@code true} if key rotation is {@link KeyRotationStrategy#INLINE}, else {@code false}
   */
  default boolean isInlineKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.INLINE;
  }

  /**
   * Checks if the key rotation strategy is {@link KeyRotationStrategy#QUEUED}.
   * @return {@code true} if key rotation is {@link KeyRotationStrategy#QUEUED}, else {@code false}
   */
  default boolean isQueuedKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.QUEUED;
  }

  /**
   * Truncate the SystemKey created time to the nearest minute.
   * @param instant A {@link java.time.Instant} object.
   * @return A {@link java.time.Instant} object truncated to the nearest minute.
   */
  default Instant truncateToSystemKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }

  /**
   Truncate the IntermediateKey created time to the nearest minute.
   * @param instant A {@link java.time.Instant} object.
   * @return A {@link java.time.Instant} object truncated to the nearest minute.
   */
  default Instant truncateToIntermediateKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }
}

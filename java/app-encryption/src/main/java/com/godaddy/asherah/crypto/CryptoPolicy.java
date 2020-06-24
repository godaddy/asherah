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
   * @param keyCreationDate the key creation date as an {@link java.time.Instant} object.
   * @return <code>true</code> if the key is expired, else <code>false</code>.
   */
  boolean isKeyExpired(Instant keyCreationDate);

  /**
   * Gets the key revoke check time in milliseconds.
   * @return the key revoke check time in milliseconds.
   */
  long getRevokeCheckPeriodMillis();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of SystemKeys.
   * @return <code>true</code> if SystemKey caching is enabled, else <code>false</code>.
   */
  boolean canCacheSystemKeys();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of IntermediateKeys.
   * @return <code>true</code> if IntermediateKey caching is enabled, else <code>false</code>.
   */
  boolean canCacheIntermediateKeys();

  /**
   * Checks if the {@link CryptoPolicy} allows caching of {@link com.godaddy.asherah.appencryption.Session}.
   * @return <code>true</code> if {@link com.godaddy.asherah.appencryption.Session} caching is enabled,
   *          else <code>false</code>.
   */
  boolean canCacheSessions();

  /**
   * Gets the maximum number of {@link com.godaddy.asherah.appencryption.Session} objects that can be cached.
   * @return The size of the session cache.
   */
  long getSessionCacheMaxSize();

  /**
   * Gets the session cache expiry time limit in milliseconds.
   * @return the session cache expiry time limit in milliseconds.
   */
  long getSessionCacheExpireMillis();

  /**
   * Checks if a notification should be sent when a DRK is using an expired IK.
   * @return <code>true</code> if notification sending is enabled, else <code>false</code>.
   */
  boolean notifyExpiredIntermediateKeyOnRead();

  /**
   * Checks if a notification should be sent when an expired SK is used during read.
   * @return <code>true</code> if notification sending is enabled, else <code>false</code>.
   */
  boolean notifyExpiredSystemKeyOnRead();

  /**
   * Get the key rotation strategy.
   * @return a {@link CryptoPolicy.KeyRotationStrategy} object.
   */
  KeyRotationStrategy keyRotationStrategy();

  /**
   * Checks if the key rotation strategy is {@link KeyRotationStrategy#INLINE}.
   * @return <code>true</code> if key rotation is {@link KeyRotationStrategy#INLINE}, else <code>false</code>
   */
  default boolean isInlineKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.INLINE;
  }

  /**
   * Checks if the key rotation strategy is {@link KeyRotationStrategy#QUEUED}.
   * @return <code>true</code> if key rotation is {@link KeyRotationStrategy#QUEUED}, else <code>false</code>
   */
  default boolean isQueuedKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.QUEUED;
  }

  /**
   * Truncate the SystemKey created time to the nearest minute.
   * @param instant a {@link java.time.Instant} object.
   * @return a {@link java.time.Instant} object.
   */
  default Instant truncateToSystemKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }

  /**
   Truncate the IntermediateKey created time to the nearest minute.
   * @param instant a {@link java.time.Instant} object.
   * @return a {@link java.time.Instant} object truncated to the nearest minute.
   */
  default Instant truncateToIntermediateKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }
}

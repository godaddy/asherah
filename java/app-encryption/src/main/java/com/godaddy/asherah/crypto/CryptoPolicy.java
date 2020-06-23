package com.godaddy.asherah.crypto;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

public interface CryptoPolicy {
  enum KeyRotationStrategy {
    INLINE,
    QUEUED
  }

  /**
   * Uses the key creation date to determine if the key is expired or not.
   * @param keyCreationDate
   * @return true if the key is expired.
   */
  boolean isKeyExpired(Instant keyCreationDate);

  /**
   * Get the revoke check period for keys
   * @return time in milliseconds
   */
  long getRevokeCheckPeriodMillis();

  /**
   * Uses the crypto policy to determine if system keys can be cached
   * @return true if system keys can be cached
   */
  boolean canCacheSystemKeys();

  /**
   * Uses the crypto policy to determine if intermediate keys can be cached
   * @return true if intermediate keys can be cached
   */
  boolean canCacheIntermediateKeys();

  /**
   * Uses the crypto policy to determine if sessions can be cached
   * @return true if sessions can be cached
   */
  boolean canCacheSessions();

  /**
   * Uses the crypto policy to determine the maximum number of sessions that can be cached
   * @return the max size of cache possible
   */
  long getSessionCacheMaxSize();

  /**
   * Uses the crypto policy to determine the time after which an item in the session cache expires
   * @return the time in milliseconds
   */
  long getSessionCacheExpireMillis();

  /**
   * Reads the intermediate key and determines if it is expired using the crytpo policy
   * @return true if the intermediate key is expired
   */
  boolean notifyExpiredIntermediateKeyOnRead();

  /**
   * Reads the system key and determines if it is expired using the crytpo policy
   * @return true if the system key is expired
   */
  boolean notifyExpiredSystemKeyOnRead();

  /**
   * Uses the crypto policy to determine the key rotation strategy
   * @return {@code KeyRotationStrategy}
   */
  KeyRotationStrategy keyRotationStrategy();

  default boolean isInlineKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.INLINE;
  }

  default boolean isQueuedKeyRotation() {
    return keyRotationStrategy() == KeyRotationStrategy.QUEUED;
  }

  default Instant truncateToSystemKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }

  default Instant truncateToIntermediateKeyPrecision(Instant instant) {
    return instant.truncatedTo(ChronoUnit.MINUTES);
  }
}

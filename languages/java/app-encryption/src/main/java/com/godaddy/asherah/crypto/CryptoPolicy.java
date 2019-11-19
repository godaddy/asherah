package com.godaddy.asherah.crypto;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

public interface CryptoPolicy {
  enum KeyRotationStrategy {
    INLINE,
    QUEUED
  }

  boolean isKeyExpired(Instant keyCreationDate);

  long getRevokeCheckPeriodMillis();

  boolean canCacheSystemKeys();

  boolean canCacheIntermediateKeys();

  boolean useSharedIntermediateKeyCache();

  long getSharedIkCacheExpireAfterAccessMillis();

  boolean notifyExpiredIntermediateKeyOnRead();

  boolean notifyExpiredSystemKeyOnRead();

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

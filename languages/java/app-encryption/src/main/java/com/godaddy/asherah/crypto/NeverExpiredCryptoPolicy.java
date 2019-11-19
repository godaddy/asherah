package com.godaddy.asherah.crypto;

import java.time.Instant;

public class NeverExpiredCryptoPolicy implements CryptoPolicy {

  @Override
  public boolean isKeyExpired(final Instant keyCreationDate) {
    return false;
  }

  @Override
  public long getRevokeCheckPeriodMillis() {
    return Long.MAX_VALUE;
  }

  @Override
  public boolean canCacheSystemKeys() {
    return true;
  }

  @Override
  public boolean canCacheIntermediateKeys() {
    return true;
  }

  @Override
  public boolean useSharedIntermediateKeyCache() {
    return false;
  }

  @Override
  public long getSharedIkCacheExpireAfterAccessMillis() {
    return Long.MAX_VALUE;
  }

  @Override
  public boolean notifyExpiredIntermediateKeyOnRead() {
    return true;
  }

  @Override
  public boolean notifyExpiredSystemKeyOnRead() {
    return true;
  }

  @Override
  public KeyRotationStrategy keyRotationStrategy() {
    return KeyRotationStrategy.INLINE;
  }

}

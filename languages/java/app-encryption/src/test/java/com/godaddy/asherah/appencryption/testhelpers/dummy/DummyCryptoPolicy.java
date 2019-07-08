package com.godaddy.asherah.appencryption.testhelpers.dummy;

import java.time.Instant;

import com.godaddy.asherah.crypto.CryptoPolicy;

public class DummyCryptoPolicy implements CryptoPolicy {

  @Override
  public boolean isKeyExpired(Instant keyCreationDate) {
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

  @Override
  public boolean isInlineKeyRotation() {
    return true;
  }

  @Override
  public boolean isQueuedKeyRotation() {
    return false;
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[policy=NeverExpire]";
  }
}

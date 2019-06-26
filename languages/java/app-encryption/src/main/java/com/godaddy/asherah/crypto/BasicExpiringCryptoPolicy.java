package com.godaddy.asherah.crypto;

import java.time.Instant;
import java.util.concurrent.TimeUnit;

/**
 * A Crypto Policy that allows easy customization of the expiration duration and caching TTL,
 * with default values for key strategy, caching, and notification options:
 * <p>
 *   - Key Rotation Strategy: Inline<br>
 *   - Caching of System and Intermediate Keys is allowed<br>
 *   - Notifications of reads using expired keys is disabled<br>
 * <p>
 * All of the default values can be modified using the optional builder methods.
 */
public class BasicExpiringCryptoPolicy implements CryptoPolicy {

  private final long keyExpirationMillis;
  private final long revokeCheckMillis;

  // NOTE: Defaults for these are taken from the Builder class, below
  private final KeyRotationStrategy keyRotationStrategy;
  private final boolean canCacheSystemKeys;
  private final boolean canCacheIntermediateKeys;
  private final boolean notifyExpiredSystemKeyOnRead;
  private final boolean notifyExpiredIntermediateKeyOnRead;


  public static KeyExpirationDaysStep newBuilder() {
    return new Builder();
  }

  BasicExpiringCryptoPolicy(final Builder builder) {
    this.keyExpirationMillis = TimeUnit.DAYS.toMillis(builder.keyExpirationDays);
    this.revokeCheckMillis = TimeUnit.MINUTES.toMillis(builder.revokeCheckMinutes);
    this.keyRotationStrategy = builder.keyRotationStrategy;
    this.canCacheSystemKeys = builder.canCacheSystemKeys;
    this.canCacheIntermediateKeys = builder.canCacheIntermediateKeys;
    this.notifyExpiredSystemKeyOnRead = builder.notifyExpiredSystemKeyOnRead;
    this.notifyExpiredIntermediateKeyOnRead = builder.notifyExpiredIntermediateKeyOnRead;
  }

  @Override
  public boolean isKeyExpired(final Instant keyCreationDate) {
    return System.currentTimeMillis() > (keyCreationDate.toEpochMilli() + keyExpirationMillis);
  }

  @Override
  public long getRevokeCheckPeriodMillis() {
    return revokeCheckMillis;
  }

  @Override
  public boolean canCacheSystemKeys() {
    return canCacheSystemKeys;
  }

  @Override
  public boolean canCacheIntermediateKeys() {
    return canCacheIntermediateKeys;
  }

  @Override
  public boolean notifyExpiredIntermediateKeyOnRead() {
    return notifyExpiredIntermediateKeyOnRead;
  }

  @Override
  public boolean notifyExpiredSystemKeyOnRead() {
    return notifyExpiredSystemKeyOnRead;
  }

  @Override
  public KeyRotationStrategy keyRotationStrategy() {
    return keyRotationStrategy;
  }


  public static final class Builder implements KeyExpirationDaysStep, RevokeCheckMinutesStep, BuildStep {
    private int keyExpirationDays;
    private int revokeCheckMinutes;

    // Set some reasonable defaults since these aren't required by the builder steps
    private KeyRotationStrategy keyRotationStrategy = KeyRotationStrategy.INLINE;
    private boolean canCacheSystemKeys = true;
    private boolean canCacheIntermediateKeys = true;
    private boolean notifyExpiredSystemKeyOnRead = false;
    private boolean notifyExpiredIntermediateKeyOnRead = false;

    @Override
    public RevokeCheckMinutesStep withKeyExpirationDays(final int days) {
      keyExpirationDays = days;
      return this;
    }

    @Override
    public BuildStep withRevokeCheckMinutes(final int minutes) {
      revokeCheckMinutes = minutes;
      return this;
    }

    @Override
    public BuildStep withRotationStrategy(final KeyRotationStrategy rotationStrategy) {
      this.keyRotationStrategy = rotationStrategy;
      return this;
    }

    @Override
    public BuildStep withCanCacheSystemKeys(final boolean cacheSystemKeys) {
      this.canCacheSystemKeys = cacheSystemKeys;
      return this;
    }

    @Override
    public BuildStep withCanCacheIntermediateKeys(final boolean cacheIntermediateKeys) {
      this.canCacheIntermediateKeys = cacheIntermediateKeys;
      return this;
    }

    @Override
    public BuildStep withNotifyExpiredSystemKeyOnRead(final boolean notify) {
      this.notifyExpiredSystemKeyOnRead = notify;
      return this;
    }

    @Override
    public BuildStep withNotifyExpiredIntermediateKeyOnRead(final boolean notify) {
      this.notifyExpiredIntermediateKeyOnRead = notify;
      return this;
    }

    @Override
    public BasicExpiringCryptoPolicy build() {
      return new BasicExpiringCryptoPolicy(this);
    }

  }

  public interface KeyExpirationDaysStep {
    RevokeCheckMinutesStep withKeyExpirationDays(int days);
  }

  public interface RevokeCheckMinutesStep {
    BuildStep withRevokeCheckMinutes(int minutes);
  }

  public interface BuildStep {
    BuildStep withRotationStrategy(KeyRotationStrategy rotationStrategy);

    BuildStep withCanCacheSystemKeys(boolean cacheSystemKeys);

    BuildStep withCanCacheIntermediateKeys(boolean cacheIntermediateKeys);

    BuildStep withNotifyExpiredSystemKeyOnRead(boolean notify);

    BuildStep withNotifyExpiredIntermediateKeyOnRead(boolean notify);

    BasicExpiringCryptoPolicy build();
  }
}

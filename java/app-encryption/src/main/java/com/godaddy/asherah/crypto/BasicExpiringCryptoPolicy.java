package com.godaddy.asherah.crypto;

import java.time.Instant;
import java.util.concurrent.TimeUnit;

/**
 * A Crypto Policy that allows easy customization of the expiration duration and caching TTL,
 * with default values for key strategy, caching, and notification options:
 * <p>
 *   - Key Rotation Strategy: Inline<br>
 *   - Caching of System and Intermediate Keys is allowed<br>
 *   - Session Caching is disabled<br>
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
  private final boolean canCacheSessions;
  private final long sessionCacheMaxSize;
  private final long sessionCacheExpireMillis;
  private final boolean notifyExpiredSystemKeyOnRead;
  private final boolean notifyExpiredIntermediateKeyOnRead;


  /**
   * Initialize a {@link BasicExpiringCryptoPolicy} builder.
   * @return The current {@code KeyExpirationDaysStep} object.
   */
  public static KeyExpirationDaysStep newBuilder() {
    return new Builder();
  }

  BasicExpiringCryptoPolicy(final Builder builder) {
    this.keyExpirationMillis = TimeUnit.DAYS.toMillis(builder.keyExpirationDays);
    this.revokeCheckMillis = TimeUnit.MINUTES.toMillis(builder.revokeCheckMinutes);
    this.keyRotationStrategy = builder.keyRotationStrategy;
    this.canCacheSystemKeys = builder.canCacheSystemKeys;
    this.canCacheIntermediateKeys = builder.canCacheIntermediateKeys;
    this.canCacheSessions = builder.canCacheSessions;
    this.sessionCacheMaxSize = builder.sessionCacheMaxSize;
    this.sessionCacheExpireMillis =
        TimeUnit.MINUTES.toMillis(builder.sessionCacheExpireMinutes);
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
  public boolean canCacheSessions() {
    return canCacheSessions;
  }

  @Override
  public long getSessionCacheMaxSize() {
    return sessionCacheMaxSize;
  }

  @Override
  public long getSessionCacheExpireMillis() {
    return sessionCacheExpireMillis;
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
    static final KeyRotationStrategy DEFAULT_KEY_ROTATION_STRATEGY = KeyRotationStrategy.INLINE;
    static final boolean DEFAULT_CAN_CACHE_SYSTEM_KEYS = true;
    static final boolean DEFAULT_CAN_CACHE_INTERMEDIATE_KEYS = true;
    static final boolean DEFAULT_CAN_CACHE_SESSIONS = false;
    static final long DEFAULT_SESSION_CACHE_SIZE = 1000;
    static final int DEFAULT_SESSION_CACHE_EXPIRY_MINUTES = 2 * 60;
    static final boolean DEFAULT_NOTIFY_EXPIRED_SYSTEM_KEY_ON_READ = false;
    static final boolean DEFAULT_NOTIFY_EXPIRED_INTERMEDIATE_KEY_ON_READ = false;

    private int keyExpirationDays;
    private int revokeCheckMinutes;

    // Set some reasonable defaults since these aren't required by the builder steps
    private KeyRotationStrategy keyRotationStrategy = DEFAULT_KEY_ROTATION_STRATEGY;
    private boolean canCacheSystemKeys = DEFAULT_CAN_CACHE_SYSTEM_KEYS;
    private boolean canCacheIntermediateKeys = DEFAULT_CAN_CACHE_INTERMEDIATE_KEYS;
    private boolean canCacheSessions = DEFAULT_CAN_CACHE_SESSIONS;
    private long sessionCacheMaxSize = DEFAULT_SESSION_CACHE_SIZE;
    private int sessionCacheExpireMinutes = DEFAULT_SESSION_CACHE_EXPIRY_MINUTES;
    private boolean notifyExpiredSystemKeyOnRead = DEFAULT_NOTIFY_EXPIRED_SYSTEM_KEY_ON_READ;
    private boolean notifyExpiredIntermediateKeyOnRead = DEFAULT_NOTIFY_EXPIRED_INTERMEDIATE_KEY_ON_READ;

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
    public BuildStep withCanCacheSessions(final boolean cacheSessions) {
      this.canCacheSessions = cacheSessions;
      return this;
    }

    @Override
    public BuildStep withSessionCacheMaxSize(final long cacheMaxSize) {
      this.sessionCacheMaxSize = cacheMaxSize;
      return this;
    }

    @Override
    public BuildStep withSessionCacheExpireMinutes(final int sessionExpireMinutes) {
      this.sessionCacheExpireMinutes = sessionExpireMinutes;
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
    /**
     * Specifies the number of days after which the keys expire.
     * @param days The expiration limit of keys.
     * @return The current {@code RevokeCheckMinutesStep} instance.
     */
    RevokeCheckMinutesStep withKeyExpirationDays(int days);
  }

  public interface RevokeCheckMinutesStep {
    /**
     * Specifies the revoke check limit (in minutes) for keys.
     * @param minutes The revoke check limit (in minutes) for keys.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withRevokeCheckMinutes(int minutes);
  }

  public interface BuildStep {
    /**
     * Specifies the key rotation strategy to use. Defaults to {@link KeyRotationStrategy#INLINE}.
     * @param rotationStrategy The strategy to use.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withRotationStrategy(KeyRotationStrategy rotationStrategy);

    /**
     * Specifies whether to cache system keys. Defaults to {@value Builder#DEFAULT_CAN_CACHE_SYSTEM_KEYS}.
     * @param cacheSystemKeys Specifies whether or not to cache system keys.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withCanCacheSystemKeys(boolean cacheSystemKeys);

    /**
     * Specifies whether to cache intermediate keys. Defaults to {@value Builder#DEFAULT_CAN_CACHE_INTERMEDIATE_KEYS}.
     * @param cacheIntermediateKeys Specifies whether or not to cache intermediate keys.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withCanCacheIntermediateKeys(boolean cacheIntermediateKeys);

    /**
     * Specifies whether to cache sessions. Defaults to {@value Builder#DEFAULT_CAN_CACHE_SESSIONS}.
     * @param cacheSessions Specifies whether or not to cache sessions.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withCanCacheSessions(boolean cacheSessions);

    /**
     * Specifies the session cache max size to use if session caching is enabled. Defaults to
     * {@value Builder#DEFAULT_SESSION_CACHE_SIZE}.
     * @param sessionCacheMaxSize The session cache max size to use.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withSessionCacheMaxSize(long sessionCacheMaxSize);

    /**
     * Specifies the session cache expiration in minutes if session caching is enabled. Defaults to
     * {@value Builder#DEFAULT_SESSION_CACHE_EXPIRY_MINUTES}.
     * @param sessionCacheExpireMinutes The session cache expiration to use, in minutes.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withSessionCacheExpireMinutes(int sessionCacheExpireMinutes);

    /**
     * Specifies whether to notify when expired system keys are read. Defaults to
     * {@value Builder#DEFAULT_NOTIFY_EXPIRED_SYSTEM_KEY_ON_READ}. NOTE: not currently implemented.
     * @param notify Specifies whether or not to notify.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withNotifyExpiredSystemKeyOnRead(boolean notify);

    /**
     * Specifies whether to notify when expired intermediate keys are read. Defaults to
     * {@value Builder#DEFAULT_NOTIFY_EXPIRED_INTERMEDIATE_KEY_ON_READ}. NOTE: not currently implemented.
     * @param notify Specifies whether or not to notify.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withNotifyExpiredIntermediateKeyOnRead(boolean notify);

    /**
     * Builds the finalized {@code BasicExpiringCryptoPolicy} with the parameters specified in the builder.
     * @return The fully instantiated {@code BasicExpiringCryptoPolicy}.
     */
    BasicExpiringCryptoPolicy build();
  }
}

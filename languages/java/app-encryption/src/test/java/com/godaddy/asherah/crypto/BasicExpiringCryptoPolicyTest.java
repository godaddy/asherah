package com.godaddy.asherah.crypto;

import org.junit.jupiter.api.Test;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.*;

class BasicExpiringCryptoPolicyTest {
  private final static int testExpirationDays = 2;
  private final static int testCachingPeriod = 30;

  BasicExpiringCryptoPolicy policy = BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(testExpirationDays)
      .withRevokeCheckMinutes(testCachingPeriod)
      .build();

  @Test
  void testIsKeyExpired() {
    Instant now = Instant.now();
    Instant before = now.minus(3, ChronoUnit.DAYS);

    assertTrue(policy.isKeyExpired(before));
  }

  @Test
  void testKeyIsNotExpired() {
    Instant now = Instant.now();
    Instant before = now.minus(1, ChronoUnit.DAYS);

    assertFalse(policy.isKeyExpired(before));
  }


  @Test
  void testRevokeCheckMillis() {
    assertEquals(TimeUnit.MINUTES.toMillis(testCachingPeriod), policy.getRevokeCheckPeriodMillis());
  }

  @Test
  void testDefaultsDontChange() {
    assertTrue(policy.canCacheSystemKeys());
    assertTrue(policy.canCacheIntermediateKeys());
    assertEquals(CryptoPolicy.KeyRotationStrategy.INLINE, policy.keyRotationStrategy());
    assertFalse(policy.notifyExpiredSystemKeyOnRead());
    assertFalse(policy.notifyExpiredIntermediateKeyOnRead());
  }

  @Test
  void testPrimaryBuilderPath() {
    BasicExpiringCryptoPolicy.KeyExpirationDaysStep builder = BasicExpiringCryptoPolicy.newBuilder();

    CryptoPolicy policy =
        builder.withKeyExpirationDays(testExpirationDays).withRevokeCheckMinutes(testCachingPeriod).build();
    assertNotNull(policy);
  }

  @Test
  void testFullBuilderPath() {
    BasicExpiringCryptoPolicy.KeyExpirationDaysStep builder = BasicExpiringCryptoPolicy.newBuilder();

    CryptoPolicy policy = builder
        .withKeyExpirationDays(testExpirationDays)
        .withRevokeCheckMinutes(testCachingPeriod)
        .withRotationStrategy(CryptoPolicy.KeyRotationStrategy.QUEUED)
        .withCanCacheSystemKeys(false)
        .withCanCacheIntermediateKeys(false)
        .withNotifyExpiredSystemKeyOnRead(true)
        .withNotifyExpiredIntermediateKeyOnRead(true)
        .build();

    assertNotNull(policy);
    assertEquals(CryptoPolicy.KeyRotationStrategy.QUEUED, policy.keyRotationStrategy());
    assertFalse(policy.canCacheSystemKeys());
    assertFalse(policy.canCacheIntermediateKeys());
    assertTrue(policy.notifyExpiredSystemKeyOnRead());
    assertTrue(policy.notifyExpiredIntermediateKeyOnRead());
  }

}

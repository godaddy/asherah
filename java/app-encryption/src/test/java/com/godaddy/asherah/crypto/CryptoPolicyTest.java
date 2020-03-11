package com.godaddy.asherah.crypto;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class CryptoPolicyTest {
  @Mock
  CryptoPolicy cryptoPolicy;

  @Test
  void isInlineKeyRotation() {
    CryptoPolicy.KeyRotationStrategy expectedStrategy = CryptoPolicy.KeyRotationStrategy.INLINE;
    when(cryptoPolicy.keyRotationStrategy()).thenReturn(expectedStrategy);
    when(cryptoPolicy.isInlineKeyRotation()).thenCallRealMethod();
    when(cryptoPolicy.isQueuedKeyRotation()).thenCallRealMethod();

    assertTrue(cryptoPolicy.isInlineKeyRotation());
    assertFalse(cryptoPolicy.isQueuedKeyRotation());
  }

  @Test
  void isQueuedKeyRotation() {
    CryptoPolicy.KeyRotationStrategy expectedStrategy = CryptoPolicy.KeyRotationStrategy.QUEUED;
    when(cryptoPolicy.keyRotationStrategy()).thenReturn(expectedStrategy);
    when(cryptoPolicy.isQueuedKeyRotation()).thenCallRealMethod();
    when(cryptoPolicy.isInlineKeyRotation()).thenCallRealMethod();

    assertTrue(cryptoPolicy.isQueuedKeyRotation());
    assertFalse(cryptoPolicy.isInlineKeyRotation());
  }

  @Test
  void testDefaultSystemKeyPrecision() {
    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenCallRealMethod();

    Instant now = Instant.now();
    assertEquals(now.truncatedTo(ChronoUnit.MINUTES), cryptoPolicy.truncateToSystemKeyPrecision(now));
  }

  @Test
  void testDefaultIntermediateKeyPrecision() {
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenCallRealMethod();

    Instant now = Instant.now();
    assertEquals(now.truncatedTo(ChronoUnit.MINUTES), cryptoPolicy.truncateToIntermediateKeyPrecision(now));
  }

}

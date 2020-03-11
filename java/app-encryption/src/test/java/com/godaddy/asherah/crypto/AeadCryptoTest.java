package com.godaddy.asherah.crypto;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.time.Instant;
import java.util.Arrays;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecretCryptoKey;
import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;

@ExtendWith(MockitoExtension.class)
class AeadCryptoTest {
  @Mock
  SecretFactory secretFactory;
  @Mock
  CryptoKey cryptoKey;
  @Mock
  Secret secret;

  @Mock
  AeadCrypto aeadCrypto;

  @Test
  void testGenerateKeyFromBytesByteArray() {
    byte[] sourceBytes = new byte[]{0, 1};
    // not sure why doXX style had to be used for this to work...shrug emoji
    doReturn(cryptoKey).when(aeadCrypto).generateKeyFromBytes(eq(sourceBytes), any());
    doCallRealMethod().when(aeadCrypto).generateKeyFromBytes(any());

    CryptoKey actualCryptoKey = aeadCrypto.generateKeyFromBytes(sourceBytes);
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testGenerateKeyFromBytesByteArrayInstant() {
    byte[] sourceBytes = new byte[]{2, 3};
    Instant now = Instant.now();
    doReturn(cryptoKey).when(aeadCrypto).generateKeyFromBytes(eq(sourceBytes), eq(now), eq(false));
    doCallRealMethod().when(aeadCrypto).generateKeyFromBytes(any(), any());

    CryptoKey actualCryptoKey = aeadCrypto.generateKeyFromBytes(sourceBytes, now);
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testGenerateKeyFromBytesByteArrayInstantBoolean() {
    byte[] sourceBytes = new byte[]{2, 3};
    byte[] clearedBytes = new byte[]{0, 0};
    Instant now = Instant.now();
    boolean revoked = true;
    when(aeadCrypto.getSecretFactory()).thenReturn(secretFactory);
    when(secretFactory.createSecret(eq(sourceBytes))).thenReturn(secret);
    when(aeadCrypto.generateKeyFromBytes(any(), any(), anyBoolean())).thenCallRealMethod();

    CryptoKey actualCryptoKey = aeadCrypto.generateKeyFromBytes(sourceBytes, now, revoked);
    assertTrue(actualCryptoKey instanceof SecretCryptoKey);
    assertEquals(now, actualCryptoKey.getCreated());
    assertEquals(revoked, actualCryptoKey.isRevoked());
    // Verify clone was used and source wasn't cleared out
    assertFalse(Arrays.equals(sourceBytes, clearedBytes));
    verify(secretFactory).createSecret(eq(sourceBytes));
  }

  @Test
  void testGenerateRandomCryptoKey() {
    // not sure why doXX style had to be used for this to work...shrug emoji
    doReturn(cryptoKey).when(aeadCrypto).generateRandomCryptoKey(any());
    doCallRealMethod().when(aeadCrypto).generateRandomCryptoKey();

    CryptoKey actualCryptoKey = aeadCrypto.generateRandomCryptoKey();
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testGenerateRandomCryptoKeyCreatedWithValidKeySize() {
    Instant now = Instant.now();
    // not sure why doXX style had to be used for this to work...shrug emoji
    doReturn(Byte.SIZE * 2).when(aeadCrypto).getKeySizeBits();
    doReturn(cryptoKey).when(aeadCrypto).generateKeyFromBytes(any(), eq(now));
    doCallRealMethod().when(aeadCrypto).generateRandomCryptoKey(any());

    CryptoKey actualCryptoKey = aeadCrypto.generateRandomCryptoKey(now);
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testGenerateRandomCryptoKeyCreatedWithInvalidKeySize() {
    // not sure why doXX style had to be used for this to work...shrug emoji
    doReturn(Byte.SIZE + 1).when(aeadCrypto).getKeySizeBits();
    doCallRealMethod().when(aeadCrypto).generateRandomCryptoKey(any());

    assertThrows(IllegalArgumentException.class, () -> aeadCrypto.generateRandomCryptoKey(Instant.now()));
  }
}

package com.godaddy.asherah.crypto.keys;


import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.securememory.Secret;

import java.time.Instant;
import java.util.function.Consumer;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class SecretCryptoKeyTest {

  @Mock
  private Secret secretMock;

  private SecretCryptoKey secretCryptoKey;
  private final Instant creationTime = Instant.now();
  private final boolean revoked = false;

  @BeforeEach
  void setUp() {
    secretCryptoKey = new SecretCryptoKey(secretMock, creationTime, revoked);
  }

  @Test
  void testConstructor() {
    assertNotNull(secretCryptoKey);
  }

  @Test
  void testConstructorUsingOtherKey() {
    Secret secretToCopy = mock(Secret.class);
    SecretCryptoKey secretCryptoKeyMock = mock(SecretCryptoKey.class);
    when(secretCryptoKeyMock.getSecret()).thenReturn(secretToCopy);
    when(secretToCopy.copySecret()).thenReturn(secretMock);
    when(secretCryptoKeyMock.getCreated()).thenReturn(creationTime);

    SecretCryptoKey secretCryptoKey = new SecretCryptoKey(secretCryptoKeyMock);

    assertNotNull(secretCryptoKey);

    Instant expectedCreationTime = creationTime;
    Instant actualCreationTime = secretCryptoKey.getCreated();
    assertEquals(expectedCreationTime, actualCreationTime);

    Secret actualSecret = secretCryptoKey.getSecret();
    assertEquals(secretMock, actualSecret);
  }

  @Test
  void testWithKeyFunction() {
    Function<byte[], ?> action = keyBytes -> null;
    secretCryptoKey.withKey(action);
    verify(secretMock).withSecretBytes(action);
  }

  @Test
  void testWithKeyConsumer() {
    Consumer<byte[]> action = bytes -> {
    };
    secretCryptoKey.withKey(action);
    verify(secretMock).withSecretBytes(action);
  }

  @Test
  void testGetCreated() {
    final Instant actualCreated = secretCryptoKey.getCreated();
    assertEquals(creationTime, actualCreated);
  }

  @Test
  void testMarkRevoked() {
    assertFalse(secretCryptoKey.isRevoked());
    secretCryptoKey.markRevoked();
    assertTrue(secretCryptoKey.isRevoked());
  }

  @Test
  void testClose() {
    secretCryptoKey.close();
    verify(secretMock).close();
  }

  @Test
  void testGetSecret() {
    Secret actualSecret = secretCryptoKey.getSecret();
    assertEquals(secretMock, actualSecret);
  }

}

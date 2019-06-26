package com.godaddy.asherah.crypto.keys;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.util.function.Consumer;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class SharedCryptoKeyTest {

  @Mock
  private CryptoKey sharedKeyMock;

  @InjectMocks
  private SharedCryptoKey sharedCryptoKey;

  @Test
  void testConstructor() {
    SharedCryptoKey sharedCryptoKey = new SharedCryptoKey(sharedKeyMock);
    assertNotNull(sharedCryptoKey);
  }

  @Test
  void testWithKeyFunction() {
    Function<byte[], ?> action = keyBytes -> null;

    sharedCryptoKey.withKey(action);
    verify(sharedKeyMock).withKey(action);
  }

  @Test
  void testWithKeyConsumer() {
    Consumer<byte[]> action = bytes -> {
    };

    sharedCryptoKey.withKey(action);
    verify(sharedKeyMock).withKey(action);
  }

  @Test
  void testGetCreated() {
    Instant expectedCreationTime = Instant.now();
    when(sharedKeyMock.getCreated()).thenReturn(expectedCreationTime);

    Instant actualCreationTime = sharedCryptoKey.getCreated();
    assertEquals(expectedCreationTime, actualCreationTime);
  }

  @Test
  void testMarkRevoked() {
    when(sharedKeyMock.isRevoked()).thenReturn(false);
    assertFalse(sharedCryptoKey.isRevoked());

    sharedCryptoKey.markRevoked();
    verify(sharedKeyMock).markRevoked();
  }

  @Test
  void testClose() {
    sharedCryptoKey.close();
    verify(sharedKeyMock, never()).close();
  }

}

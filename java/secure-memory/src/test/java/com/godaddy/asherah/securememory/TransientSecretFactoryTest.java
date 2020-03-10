package com.godaddy.asherah.securememory;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecret;

class TransientSecretFactoryTest {
  TransientSecretFactory transientSecretFactory;

  @BeforeEach
  void setUp() {
    transientSecretFactory = new TransientSecretFactory();
  }

  @Test
  void testCreateSecretByteArray() {
    Secret secret = transientSecretFactory.createSecret(new byte[] {0, 1});
    assertTrue(secret instanceof ProtectedMemorySecret);
  }

  @Test
  void testCreateSecretCharArray() {
    Secret secret = transientSecretFactory.createSecret(new char[] {'a', 'b'});
    assertTrue(secret instanceof ProtectedMemorySecret);
  }

}

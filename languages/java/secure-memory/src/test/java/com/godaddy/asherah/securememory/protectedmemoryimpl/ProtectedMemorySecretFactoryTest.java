package com.godaddy.asherah.securememory.protectedmemoryimpl;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.godaddy.asherah.securememory.Secret;

class ProtectedMemorySecretFactoryTest {
  ProtectedMemorySecretFactory protectedMemorySecretFactory;

  @BeforeEach
  void setUp() throws Exception {
    protectedMemorySecretFactory = new ProtectedMemorySecretFactory();
  }

  // TODO When Mockito supports static methods or PowerMockito supports junit5, mock platform calls to test these flows
  @Test
  void testProtectedMemorySecretFactoryWithMac() {
  }

  @Test
  void testProtectedMemorySecretFactoryWithLinux() {
  }

  @Test
  void testProtectedMemorySecretFactoryWithWindowsShouldFail() {
  }

  @Test
  void testCreateSecretByteArray() {
    Secret secret = protectedMemorySecretFactory.createSecret(new byte[] {0, 1});
    assertTrue(secret instanceof ProtectedMemorySecret);
  }

  @Test
  void testCreateSecretCharArray() {
    Secret secret = protectedMemorySecretFactory.createSecret(new char[] {'a', 'b'});
    assertTrue(secret instanceof ProtectedMemorySecret);
  }

}

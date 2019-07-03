package com.godaddy.asherah.securememory.protectedmemoryimpl;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class ProtectedMemoryAllocationFailedTest {
  static String message = "Test Message";

  @Test
  void protectedMemoryAllocationFailedConstructorTest() {
    ProtectedMemoryAllocationFailed ex = new ProtectedMemoryAllocationFailed(message);
    assertEquals(message, ex.getMessage());
  }
}

package com.godaddy.asherah.securememory.protectedmemoryimpl;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class ProtectedMemoryExceptionTest {
  static String message = "Test Message";

  @Test
  void protectedMemoryExceptionConstructorTest() {
    ProtectedMemoryException ex = new ProtectedMemoryException(message);
    assertEquals(message, ex.getMessage());
  }
}

package com.godaddy.asherah.securememory.protectedmemoryimpl;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class MemoryLimitExceptionTest {
  static String message = "Test Message";

  @Test
  void memoryLimitExceptionConstructorTest() {
    MemoryLimitException ex = new MemoryLimitException(message);
    assertEquals(message, ex.getMessage());
  }
}

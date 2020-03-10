package com.godaddy.asherah.securememory.protectedmemoryimpl;

public class MemoryLimitException extends RuntimeException {

  public MemoryLimitException(final String message) {
    super(message);
  }
}

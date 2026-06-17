package com.godaddy.asherah.securememory.ffmimpl;

/**
 * Exception thrown when memory allocation exceeds system limits.
 */
public class FfmMemoryLimitException extends RuntimeException {

  public FfmMemoryLimitException(final String message) {
    super(message);
  }
}


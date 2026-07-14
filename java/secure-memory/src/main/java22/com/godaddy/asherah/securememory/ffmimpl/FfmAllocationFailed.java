package com.godaddy.asherah.securememory.ffmimpl;

/**
 * Exception thrown when FFM memory allocation fails.
 */
public class FfmAllocationFailed extends RuntimeException {

  public FfmAllocationFailed(final String message) {
    super(message);
  }

  public FfmAllocationFailed(final String message, final Throwable cause) {
    super(message, cause);
  }
}


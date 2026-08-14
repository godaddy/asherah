package com.godaddy.asherah.securememory.ffmimpl;

/**
 * Exception thrown when an FFM native operation fails.
 */
public class FfmOperationFailed extends RuntimeException {

  public FfmOperationFailed(final String operation, final int errorCode) {
    super(String.format("FFM operation '%s' failed with error code: %d", operation, errorCode));
  }

  public FfmOperationFailed(final String operation, final Throwable cause) {
    super(String.format("FFM operation '%s' failed", operation), cause);
  }

  public FfmOperationFailed(final String message) {
    super(message);
  }
}


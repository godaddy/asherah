package com.godaddy.asherah.crypto.exceptions;

public class CipherNotSupportedException extends RuntimeException {

  /**
   * Creates a new {@code CipherNotSupportedException}. This signals that the cipher being used is not supported by the
   * library
   * @param message The detailed exception message.
   */
  public CipherNotSupportedException(final String message) {
  }

  /**
   * Creates a new {@code CipherNotSupportedException}. This signals that the cipher being used is not supported by the
   * library
   * @param message The detailed exception message.
   * @param e The actual {@link java.lang.Exception} raised.
   */
  public CipherNotSupportedException(final String message, final Exception e) {
  }
}

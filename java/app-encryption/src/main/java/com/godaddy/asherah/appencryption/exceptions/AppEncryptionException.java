package com.godaddy.asherah.appencryption.exceptions;

public class AppEncryptionException extends RuntimeException {

  /**
   * Constructor for AppEncryptionException.
   *
   * @param message The exception message.
   */
  public AppEncryptionException(final String message) {
    super(message);
  }

  /**
   * Constructor for AppEncryptionException.
   *
   * @param message The exception message.
   * @param cause The {@link java.lang.Exception} object.
   */
  public AppEncryptionException(final String message, final Exception cause) {
    super(message, cause);
  }
}

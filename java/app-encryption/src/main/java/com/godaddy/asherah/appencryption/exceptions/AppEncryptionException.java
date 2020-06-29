package com.godaddy.asherah.appencryption.exceptions;

public class AppEncryptionException extends RuntimeException {

  /**
   * Creates a new {@code AppEncryptionException}. This signals that a
   * {@link com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption} exception has occurred.
   *
   * @param message The detailed exception message.
   */
  public AppEncryptionException(final String message) {
    super(message);
  }

  /**
   * Creates a new {@code AppEncryptionException}. This signals that a
   * {@link com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption} exception has occurred.
   *
   * @param message The detailed exception message.
   * @param cause The actual {@link java.lang.Exception} raised.
   */
  public AppEncryptionException(final String message, final Exception cause) {
    super(message, cause);
  }
}

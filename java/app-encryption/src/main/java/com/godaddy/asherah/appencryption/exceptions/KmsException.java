package com.godaddy.asherah.appencryption.exceptions;

public class KmsException extends AppEncryptionException {
  /**
   * Constructor for KmsException.
   * @param message The exception message.
   */
  public KmsException(final String message) {
    super(message);
  }
}

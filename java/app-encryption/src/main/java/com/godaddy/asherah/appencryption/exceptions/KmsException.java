package com.godaddy.asherah.appencryption.exceptions;

public class KmsException extends AppEncryptionException {
  /**
   * Creates a new {@code KmsException}. This signals that a
   * {@link com.godaddy.asherah.appencryption.kms.KeyManagementService} exception has occurred.
   *
   * @param message The detailed exception message.
   */
  public KmsException(final String message) {
    super(message);
  }
}

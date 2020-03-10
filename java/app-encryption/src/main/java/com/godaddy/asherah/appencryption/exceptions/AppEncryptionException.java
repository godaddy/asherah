package com.godaddy.asherah.appencryption.exceptions;

public class AppEncryptionException extends RuntimeException {

  public AppEncryptionException(final String message) {
    super(message);
  }

  public AppEncryptionException(final String message, final Exception cause) {
    super(message, cause);
  }
}

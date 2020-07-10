package com.godaddy.asherah.appencryption.exceptions;

public class MetadataMissingException extends AppEncryptionException {

  /**
   * Creates a new {@code MetadataMissingException}. This signals that a
   * {@link com.godaddy.asherah.appencryption.persistence.Metastore} exception has occurred and some key metadata is
   * missing.
   *
   * @param message The detailed exception message.
   */
  public MetadataMissingException(final String message) {
    super(message);
  }
}

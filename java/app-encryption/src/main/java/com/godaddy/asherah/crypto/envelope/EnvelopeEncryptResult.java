package com.godaddy.asherah.crypto.envelope;

public class EnvelopeEncryptResult {
  private byte[] cipherText;
  private byte[] encryptedKey;
  private Object userState; // TODO Clarify intended usage of this. Consider renaming and making a generic

  /**
   * Getter for the field {@code cipherText}.
   * @return the cipher text/encrypted payload in the DRR.
   */
  public byte[] getCipherText() {
    return cipherText;
  }

  /**
   * Setter for the field {@code cipherText}.
   * @param cipherText the cipher text/encrypted payload.
   */
  public void setCipherText(final byte[] cipherText) {
    this.cipherText = cipherText;
  }

  /**
   * Getter for the field {@code encryptedKey}.
   * @return the encrypted key in the DRR.
   */
  public byte[] getEncryptedKey() {
    return encryptedKey;
  }

  /**
   * Setter for the field {@code encryptedKey}.
   * @param key the encrypted key.
   */
  public void setEncryptedKey(final byte[] key) {
    encryptedKey = key;
  }

  /**
   * Getter for the field {@code userState}.
   * @return the {@link com.godaddy.asherah.appencryption.envelope.KeyMeta} in the DRR.
   */
  public Object getUserState() {
    return userState;
  }

  /**
   * Setter for the field {@code userState}.
   * @param state the @link com.godaddy.asherah.appencryption.envelope.KeyMeta}.
   */
  public void setUserState(final Object state) {
    userState = state;
  }
}

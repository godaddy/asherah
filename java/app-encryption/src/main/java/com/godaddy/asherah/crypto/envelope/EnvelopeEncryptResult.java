package com.godaddy.asherah.crypto.envelope;

public class EnvelopeEncryptResult {
  private byte[] cipherText;
  private byte[] encryptedKey;
  private Object userState; // TODO Clarify intended usage of this. Consider renaming and making a generic

  public byte[] getCipherText() {
    return cipherText;
  }

  public void setCipherText(final byte[] cipherText) {
    this.cipherText = cipherText;
  }

  public byte[] getEncryptedKey() {
    return encryptedKey;
  }

  public void setEncryptedKey(final byte[] key) {
    encryptedKey = key;
  }

  public Object getUserState() {
    return userState;
  }

  public void setUserState(final Object state) {
    userState = state;
  }
}

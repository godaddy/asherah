package com.godaddy.asherah.crypto.envelope;

import static com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils.wipeByteArray;

import java.time.Instant;

import com.godaddy.asherah.crypto.AeadCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

public abstract class AeadEnvelopeCrypto extends AeadCrypto {

  /**
   * Encrypts a {@link CryptoKey} with another {@link CryptoKey}.
   * @param key The key to encrypt.
   * @param keyEncryptionKey The key encryption key.
   * @return An encrypted key
   */
  public byte[] encryptKey(final CryptoKey key, final CryptoKey keyEncryptionKey) {
    return key.withKey(keyBytes -> {
      return encrypt(keyBytes, keyEncryptionKey);
    });
  }

  /**
   * Decrypts an encrypted key.
   * @param encryptedKey An encrypted key.
   * @param encryptedKeyCreated The creation time of the encrypted key.
   * @param keyEncryptionKey The key encryption key.
   * @return A decrypted {@link CryptoKey} object.
   */
  public CryptoKey decryptKey(final byte[] encryptedKey, final Instant encryptedKeyCreated,
      final CryptoKey keyEncryptionKey) {
    return decryptKey(encryptedKey, encryptedKeyCreated, keyEncryptionKey, false);
  }

  /**
   * Decrypts an encrypted key.
   * @param encryptedKey An encrypted key.
   * @param encryptedKeyCreated The creation time of the encrypted key.
   * @param keyEncryptionKey The key encryption key.
   * @param revoked The revocation status of the key.
   * @return A decrypted {@link CryptoKey} object.
   */
  public CryptoKey decryptKey(final byte[] encryptedKey, final Instant encryptedKeyCreated,
      final CryptoKey keyEncryptionKey, final boolean revoked) {
    //Invert this into functional
    byte[] decryptedKey = decrypt(encryptedKey, keyEncryptionKey);
    try {
      return generateKeyFromBytes(decryptedKey, encryptedKeyCreated, revoked);
    }
    finally {
      wipeByteArray(decryptedKey);
    }
  }

  /**
   * Encrypts the payload and the key to create the data row record.
   * @param plainText the payload to be encrypted.
   * @param keyEncryptionKey the key encryption key.
   * @return a {@link EnvelopeEncryptResult} object/data row record (DRR).
   */
  public EnvelopeEncryptResult envelopeEncrypt(final byte[] plainText, final CryptoKey keyEncryptionKey) {
    return envelopeEncrypt(plainText, keyEncryptionKey, null);
  }


  /**
   * Encrypts the payload and the key to create the data row record.
   * @param plainText the payload to be encrypted.
   * @param keyEncryptionKey the key encryption key.
   * @param userState the KeyMeta for the <code>keyEncryptionKey</code>.
   * @return a {@link EnvelopeEncryptResult} object/data row record (DRR).
   */
  public EnvelopeEncryptResult envelopeEncrypt(final byte[] plainText, final CryptoKey keyEncryptionKey,
      final Object userState) {
    try (CryptoKey dataEncryptionKey = generateKey()) {
      EnvelopeEncryptResult result = new EnvelopeEncryptResult();
      result.setCipherText(encrypt(plainText, dataEncryptionKey));
      result.setEncryptedKey(encryptKey(dataEncryptionKey, keyEncryptionKey));
      result.setUserState(userState);
      return result;
    }
  }

  /**
   * Decrypts the encryptedKey and then uses the decrypted key to decrypt the encrypted payload in the data row record.
   * @param cipherText the encrypted payload.
   * @param encryptedKey the encrypted key.
   * @param keyCreated the creation time of the data row record.
   * @param keyEncryptionKey the key encryption key.
   * @return the decrypted payload.
   */
  public byte[] envelopeDecrypt(final byte[] cipherText, final byte[] encryptedKey,
      final Instant keyCreated, final CryptoKey keyEncryptionKey) {
    try (CryptoKey plaintextKey = decryptKey(encryptedKey, keyCreated, keyEncryptionKey)) {
      return decrypt(cipherText, plaintextKey);
    }
  }
}

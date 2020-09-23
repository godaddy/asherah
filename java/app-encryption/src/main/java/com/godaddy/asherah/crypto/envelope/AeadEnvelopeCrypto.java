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
   * @return The encrypted key bytes.
   */
  public byte[] encryptKey(final CryptoKey key, final CryptoKey keyEncryptionKey) {
    return key.withKey(keyBytes -> {
      return encrypt(keyBytes, keyEncryptionKey);
    });
  }

  /**
   * Decrypts an encrypted key.
   * @param encryptedKey The encrypted key bytes.
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
   * @param encryptedKey The encrypted key bytes.
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
   * @param plainText The payload to be encrypted.
   * @param keyEncryptionKey The key encryption key.
   * @return An {@link EnvelopeEncryptResult} object/data row record (DRR).
   */
  public EnvelopeEncryptResult envelopeEncrypt(final byte[] plainText, final CryptoKey keyEncryptionKey) {
    return envelopeEncrypt(plainText, keyEncryptionKey, null);
  }


  /**
   * Encrypts the payload and the key to create the data row record.
   * @param plainText The payload to be encrypted.
   * @param keyEncryptionKey The key encryption key.
   * @param userState The KeyMeta for the {@code keyEncryptionKey}.
   * @return An {@link EnvelopeEncryptResult} object/data row record (DRR).
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
   * @param cipherText The encrypted payload.
   * @param encryptedKey The encrypted key.
   * @param keyCreated The creation time of the data row record.
   * @param keyEncryptionKey The key encryption key.
   * @return The decrypted payload.
   */
  public byte[] envelopeDecrypt(final byte[] cipherText, final byte[] encryptedKey,
      final Instant keyCreated, final CryptoKey keyEncryptionKey) {
    try (CryptoKey plaintextKey = decryptKey(encryptedKey, keyCreated, keyEncryptionKey)) {
      return decrypt(cipherText, plaintextKey);
    }
  }
}

package com.godaddy.asherah.appencryption.kms;

import java.time.Instant;
import java.util.function.BiFunction;

import com.godaddy.asherah.crypto.keys.CryptoKey;

public interface KeyManagementService {
  /**
   * Encrypts a key using the current {@link KeyManagementService}.
   * @param key The key to encrypt.
   * @return An encrypted key.
   */
  byte[] encryptKey(CryptoKey key);

  /**
   * Decrypts an encrypted key using the current {@link KeyManagementService}.
   * @param keyCipherText The encrypted key.
   * @param keyCreated The creation time of the encrypted key.
   * @param revoked the revocation status of the key.
   * @return a decrypted {@link CryptoKey}.
   */
  CryptoKey decryptKey(byte[] keyCipherText, Instant keyCreated, boolean revoked);

  /**
   * Decrypts a key and applies the provided function to the key.
   * @param keyCipherText The encrypted key.
   * @param keyCreated The creation time of the encrypted key.
   * @param revoked the revocation status of the key.
   * @param actionWithDecryptedKey a {@link java.util.function.BiFunction} to be applied to the decrypted key.
   * @param <T> The type being used to return the result from the function.
   * @return the function result.
   */
  default <T> T withDecryptedKey(final byte[] keyCipherText, final Instant keyCreated, final boolean revoked,
                                final BiFunction<CryptoKey, Instant, T> actionWithDecryptedKey) {
    try (CryptoKey key = decryptKey(keyCipherText, keyCreated, revoked)) {
      return actionWithDecryptedKey.apply(key, keyCreated);
    }
  }
}

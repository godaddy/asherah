package com.godaddy.asherah.appencryption.kms;

import java.time.Instant;
import java.util.function.BiFunction;

import com.godaddy.asherah.crypto.keys.CryptoKey;

public interface KeyManagementService {
  byte[] encryptKey(CryptoKey key);

  CryptoKey decryptKey(byte[] keyCipherText, Instant keyCreated, boolean revoked);

  default <T> T withDecryptedKey(final byte[] keyCipherText, final Instant keyCreated, final boolean revoked,
                                final BiFunction<CryptoKey, Instant, T> actionWithDecryptedKey) {
    try (CryptoKey key = decryptKey(keyCipherText, keyCreated, revoked)) {
      return actionWithDecryptedKey.apply(key, keyCreated);
    }
  }

}

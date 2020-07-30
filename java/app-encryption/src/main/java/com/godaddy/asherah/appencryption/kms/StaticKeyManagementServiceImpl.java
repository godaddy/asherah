package com.godaddy.asherah.appencryption.kms;

import java.time.Instant;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecretCryptoKey;
import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.TransientSecretFactory;

/**
 * An implementation of {@link KeyManagementService} that uses {@link BouncyAes256GcmCrypto} to encrypt/decrypt keys.
 * Note: This should NEVER be used in production.
 */
public class StaticKeyManagementServiceImpl implements KeyManagementService {
  private final CryptoKey encryptionKey;
  private final BouncyAes256GcmCrypto crypto = new BouncyAes256GcmCrypto();

  /**
   * Creates a new {@code StaticKeyManagementServiceImpl} instance. This implementation of {@link KeyManagementService}
   * uses a static master key to encrypt the system keys.
   *
   * @param key The static master key.
   */
  public StaticKeyManagementServiceImpl(final String key) {
    byte[] keyBytes = key.getBytes();
    Secret secretKey = new TransientSecretFactory().createSecret(keyBytes);

    encryptionKey = new SecretCryptoKey(secretKey, Instant.now(), false);
  }

  @Override
  public byte[] encryptKey(final CryptoKey key) {
    return crypto.encryptKey(key, encryptionKey);
  }

  @Override
  public CryptoKey decryptKey(final byte[] keyCipherText, final Instant keyCreated, final boolean revoked) {
    return crypto.decryptKey(keyCipherText, keyCreated, encryptionKey, revoked);
  }
}

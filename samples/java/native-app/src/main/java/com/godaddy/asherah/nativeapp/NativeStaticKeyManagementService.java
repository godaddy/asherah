package com.godaddy.asherah.nativeapp;

import java.time.Instant;

import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.crypto.engine.CryptoEngineType;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecretCryptoKey;
import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.TransientSecretFactory;

/**
 * Static KMS implementation for GraalVM native-image.
 *
 * <p>This implementation directly uses FFM for secure memory instead of going through
 * the reflection-based TransientSecretFactory. This ensures the FFM classes are
 * included in the native-image during AOT compilation.
 *
 * <p><b>Note:</b> This is for demonstration purposes only. In production, use
 * proper key management services like AWS KMS.
 */
public class NativeStaticKeyManagementService implements KeyManagementService {

  private final CryptoKey encryptionKey;
  private final AeadEnvelopeCrypto crypto;

  /**
   * Creates a new NativeStaticKeyManagementService with the given static key.
   *
   * @param key The static master key.
   */
  public NativeStaticKeyManagementService(final String key) {
    this(key, CryptoEngineType.JDK);
  }

  /**
   * Creates a new NativeStaticKeyManagementService with the given static key.
   *
   * @param key The static master key.
   * @param cryptoEngineType The crypto engine to use (should be JDK for native-image).
   */
  public NativeStaticKeyManagementService(final String key, final CryptoEngineType cryptoEngineType) {
    byte[] keyBytes = key.getBytes();

    // Use TransientSecretFactory which auto-selects FFM on Java 22+ or JNA as fallback
    // This works in both native-image (where FFM classes are directly available)
    // and JVM mode (where multi-release JAR provides FFM classes)
    SecretFactory secretFactory = new TransientSecretFactory();
    Secret secretKey = secretFactory.createSecret(keyBytes);

    encryptionKey = new SecretCryptoKey(secretKey, Instant.now(), false);
    crypto = cryptoEngineType.createCryptoEngine();
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


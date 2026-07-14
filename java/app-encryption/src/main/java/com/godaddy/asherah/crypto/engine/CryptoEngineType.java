package com.godaddy.asherah.crypto.engine;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.engine.jdk.JdkAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;

/**
 * Enumeration of available cryptographic engine implementations.
 * This allows selecting between different crypto providers while maintaining
 * compatibility with existing encrypted data.
 */
public enum CryptoEngineType {

  /**
   * BouncyCastle-based AES-256-GCM implementation.
   * This is the original implementation and is well-tested.
   */
  BOUNCY_CASTLE {
    @Override
    public AeadEnvelopeCrypto createCryptoEngine() {
      return new BouncyAes256GcmCrypto();
    }
  },

  /**
   * JDK-based AES-256-GCM implementation using {@code javax.crypto}.
   * This implementation is recommended for GraalVM native-image compilation
   * as it doesn't require reflection configuration.
   */
  JDK {
    @Override
    public AeadEnvelopeCrypto createCryptoEngine() {
      return new JdkAes256GcmCrypto();
    }
  };

  /**
   * The default crypto engine type.
   * Currently defaults to BouncyCastle for backward compatibility.
   */
  public static final CryptoEngineType DEFAULT = BOUNCY_CASTLE;

  /**
   * Creates a new instance of the crypto engine for this type.
   *
   * @return A new {@link AeadEnvelopeCrypto} instance.
   */
  public abstract AeadEnvelopeCrypto createCryptoEngine();
}


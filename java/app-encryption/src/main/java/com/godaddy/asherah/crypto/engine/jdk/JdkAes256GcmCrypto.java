package com.godaddy.asherah.crypto.engine.jdk;

import javax.crypto.Cipher;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

/**
 * JDK-based implementation of AES-256-GCM encryption using {@code javax.crypto}.
 * This implementation is compatible with GraalVM native-image compilation and
 * produces output that is interoperable with
 * {@link com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto}.
 */
public class JdkAes256GcmCrypto extends AeadEnvelopeCrypto {

  private static final int NONCE_SIZE_BITS = 96;
  private static final int MAC_SIZE_BITS = 128;
  private static final int KEY_SIZE_BITS = 256;
  private static final String ALGORITHM = "AES/GCM/NoPadding";
  private static final String KEY_ALGORITHM = "AES";

  /**
   * Creates a new {@link JdkAes256GcmCrypto} instance.
   */
  public JdkAes256GcmCrypto() {
    super();
  }

  @Override
  public byte[] encrypt(final byte[] input, final CryptoKey key) {
    byte[] nonce = generateNonce();

    return key.withKey(keyBytes -> {
      try {
        Cipher cipher = Cipher.getInstance(ALGORITHM);
        SecretKeySpec keySpec = new SecretKeySpec(keyBytes, KEY_ALGORITHM);
        GCMParameterSpec gcmSpec = new GCMParameterSpec(MAC_SIZE_BITS, nonce);
        cipher.init(Cipher.ENCRYPT_MODE, keySpec, gcmSpec);

        // JDK GCM output is: ciphertext || authTag
        byte[] cipherTextWithTag = cipher.doFinal(input);

        // Append nonce at the end (same format as BouncyCastle implementation)
        byte[] output = new byte[cipherTextWithTag.length + nonce.length];
        System.arraycopy(cipherTextWithTag, 0, output, 0, cipherTextWithTag.length);
        appendNonce(output, nonce);

        return output;
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during encryption", e);
      }
    });
  }

  @Override
  public byte[] decrypt(final byte[] input, final CryptoKey key) {
    byte[] nonce = getAppendedNonce(input);
    int cipherTextLength = input.length - nonce.length;

    return key.withKey(keyBytes -> {
      try {
        Cipher cipher = Cipher.getInstance(ALGORITHM);
        SecretKeySpec keySpec = new SecretKeySpec(keyBytes, KEY_ALGORITHM);
        GCMParameterSpec gcmSpec = new GCMParameterSpec(MAC_SIZE_BITS, nonce);
        cipher.init(Cipher.DECRYPT_MODE, keySpec, gcmSpec);

        return cipher.doFinal(input, 0, cipherTextLength);
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during decryption", e);
      }
    });
  }

  @Override
  protected int getNonceSizeBits() {
    return NONCE_SIZE_BITS;
  }

  @Override
  protected int getKeySizeBits() {
    return KEY_SIZE_BITS;
  }

  @Override
  protected int getMacSizeBits() {
    return MAC_SIZE_BITS;
  }
}


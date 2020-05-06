package com.godaddy.asherah.crypto;

import static com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils.wipeByteArray;
import static java.lang.System.arraycopy;

import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecretCryptoKey;
import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.TransientSecretFactory;

import java.security.SecureRandom;
import java.time.Instant;

public abstract class AeadCrypto {
  // TODO Add ThreadLocal wrapper. Maybe consider adding periodic reseeding of this
  protected static final SecureRandom CRYPTO_RANDOM = new SecureRandom();

  private final SecretFactory secretFactory;
  private final NonceGenerator nonceGenerator;

  protected abstract int getNonceSizeBits();

  protected abstract int getKeySizeBits();

  protected abstract int getMacSizeBits();

  public abstract byte[] encrypt(byte[] input, CryptoKey key);

  public abstract byte[] decrypt(byte[] input, CryptoKey key);

  protected AeadCrypto() {
    secretFactory = new TransientSecretFactory();
    nonceGenerator = new NonceGenerator();
  }

  protected SecretFactory getSecretFactory() {
    return secretFactory;
  }

  protected byte[] getAppendedNonce(final byte[] cipherTextAndNonce) {
    int nonceByteSize = getNonceSizeBits() / Byte.SIZE;
    byte[] nonce = new byte[nonceByteSize];
    arraycopy(cipherTextAndNonce, cipherTextAndNonce.length - nonceByteSize, nonce, 0, nonceByteSize);
    return nonce;
  }

  protected void appendNonce(final byte[] cipherText, final byte[] nonce) {
    arraycopy(nonce, 0, cipherText, cipherText.length - nonce.length, nonce.length);
  }

  protected byte[] generateNonce() {
    return nonceGenerator.createNonce(getNonceSizeBits());
  }

  public CryptoKey generateKey() {
    return generateRandomCryptoKey();
  }

  public CryptoKey generateKey(final Instant created) {
    return generateRandomCryptoKey(created);
  }

  /**
   * Generates a <code>CryptoKey</code> using the provided source bytes. <b>NOTE</b>: you MUST wipe out the source
   * bytes after the completion of this call!
   * @param sourceBytes the bytes to use for generating <code>CryptoKey</code>
   * @return a generated <code>CryptoKey</code>
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes) {
    return generateKeyFromBytes(sourceBytes, Instant.now());
  }

  /**
   * Generates a <code>CryptoKey</code> using the provided source bytes and created time. <b>NOTE</b>: you MUST wipe
   * out the source bytes after the completion of this call!
   * @param sourceBytes the bytes to use for generating <code>CryptoKey</code>
   * @param created the time to associate the generated <code>CryptoKey</code> with
   * @return a generated <code>CryptoKey</code>
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes, final Instant created) {
    return generateKeyFromBytes(sourceBytes, created, false);
  }

  /**
   * Generates a <code>CryptoKey</code> using the provided source bytes, created time, and revoked flag. <b>NOTE</b>:
   * you MUST wipe out the source bytes after the completion of this call!
   * @param sourceBytes the bytes to use for generating <code>CryptoKey</code>
   * @param created the time to associate the generated <code>CryptoKey</code> with
   * @param revoked the flag to set while generating <code>CryptoKey</code>
   * @return a generated new <code>CryptoKey</code>
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes, final Instant created, final boolean revoked) {
    byte[] clonedBytes = sourceBytes.clone();
    Secret newKeySecret = getSecretFactory().createSecret(clonedBytes);

    return new SecretCryptoKey(newKeySecret, created, revoked);
  }

  /**
   * Generates a random <code>CryptoKey</code> using the current time as the created time.
   * @return a generated random <code>CryptoKey</code>
   */
  protected CryptoKey generateRandomCryptoKey() {
    return generateRandomCryptoKey(Instant.now());
  }

  /**
   * Generates a random <code>CryptoKey</code> using the given time as the created time.
   * @param created the time to associate the generated <code>CryptoKey</code> with
   * @return a generated random <code>CryptoKey</code>
   */
  protected CryptoKey generateRandomCryptoKey(final Instant created) {
    int keyLengthBits = getKeySizeBits();

    if (keyLengthBits % Byte.SIZE != 0) {
      throw new IllegalArgumentException("Invalid key length " + keyLengthBits);
    }

    byte[] keyBytes = new byte[keyLengthBits / Byte.SIZE];
    CRYPTO_RANDOM.nextBytes(keyBytes);
    try {
      return generateKeyFromBytes(keyBytes, created);
    }
    finally {
      wipeByteArray(keyBytes);
    }
  }
}


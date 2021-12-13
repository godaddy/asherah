package com.godaddy.asherah.crypto;

import static com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils.wipeByteArray;
import static java.lang.System.arraycopy;

import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecretCryptoKey;
import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.TransientSecretFactory;

import java.io.InputStream;
import java.io.OutputStream;
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

  /**
   * Encrypts the provided payload.
   * @param input Payload bytes to be encrypted.
   * @param key The {@link CryptoKey} to encrypt the payload with.
   * @return An encrypted payload.
   */
  public abstract byte[] encrypt(byte[] input, CryptoKey key);

  /**
   * Decrypts an encrypted payload.
   * @param input The encrypted payload.
   * @param key The {@link CryptoKey} to decrypt the payload with.
   * @return A decrypted payload.
   */
  public abstract byte[] decrypt(byte[] input, CryptoKey key);

  /**
   *
   * @param inputStream
   * @param outputStream
   * @param key
   */
  public abstract void encryptStream(InputStream inputStream, OutputStream outputStream, CryptoKey key);

  /**
   *
   * @param inputStream
   * @param outStream
   * @param key
   */
  public abstract void decryptStream(InputStream inputStream, OutputStream outStream, CryptoKey key);

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

  /**
   * Generates a new {@link CryptoKey}.
   * @return A newly generated {@link CryptoKey}.
   */
  public CryptoKey generateKey() {
    return generateRandomCryptoKey();
  }

  /**
   * Generates a new {@link CryptoKey} using the provided time stamp.
   * @param created The timestamp to be used for key creation.
   * @return A newly generated {@link CryptoKey}.
   */
  public CryptoKey generateKey(final Instant created) {
    return generateRandomCryptoKey(created);
  }

  /**
   * Generates a {@link CryptoKey} using the provided source bytes. <b>NOTE</b>: you MUST wipe out the source
   * bytes after the completion of this call!
   * @param sourceBytes The bytes to use for generating {@link CryptoKey}.
   * @return A {@link CryptoKey} generated from the {@code sourceBytes}.
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes) {
    return generateKeyFromBytes(sourceBytes, Instant.now());
  }

  /**
   * Generates a {@link CryptoKey} using the provided source bytes and created time. <b>NOTE</b>: you MUST wipe
   * out the source bytes after the completion of this call!
   * @param sourceBytes The bytes to use for generating {@link CryptoKey}.
   * @param created The time to associate the generated {@link CryptoKey} with.
   * @return A {@link CryptoKey} generated from the {@code sourceBytes}.
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes, final Instant created) {
    return generateKeyFromBytes(sourceBytes, created, false);
  }

  /**
   * Generates a {@link CryptoKey} using the provided source bytes, created time, and revoked flag. <b>NOTE</b>:
   * you MUST wipe out the source bytes after the completion of this call!
   * @param sourceBytes The bytes to use for generating {@link CryptoKey}.
   * @param created The time to associate the generated {@link CryptoKey} with.
   * @param revoked The flag to set while generating {@link CryptoKey}.
   * @return A {@link CryptoKey} generated from the {@code sourceBytes}.
   */
  public CryptoKey generateKeyFromBytes(final byte[] sourceBytes, final Instant created, final boolean revoked) {
    byte[] clonedBytes = sourceBytes.clone();
    Secret newKeySecret = getSecretFactory().createSecret(clonedBytes);

    return new SecretCryptoKey(newKeySecret, created, revoked);
  }

  /**
   * Generates a random {@link CryptoKey} using the current time as the created time.
   * @return A newly generated {@link CryptoKey}.
   */
  protected CryptoKey generateRandomCryptoKey() {
    return generateRandomCryptoKey(Instant.now());
  }

  /**
   * Generates a random {@link CryptoKey} using the given time as the created time.
   * @param created The time to associate the generated {@link CryptoKey} with.
   * @return A generated random {@link CryptoKey}.
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

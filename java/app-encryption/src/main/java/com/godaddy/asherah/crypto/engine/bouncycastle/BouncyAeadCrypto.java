package com.godaddy.asherah.crypto.engine.bouncycastle;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.securememory.Debug;

import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public abstract class BouncyAeadCrypto extends AeadEnvelopeCrypto {
  private static final Logger LOG = LoggerFactory.getLogger(BouncyAeadCrypto.class);

  //Do NOT modify the size of the nonce unless you've read NIST 800-38D
  private static final int NonceSizeBits = 96;
  private static final int MacSizeBits = 128;
  private static final int KeySizeBits = 256;

  protected abstract AEADCipher getNewAeadBlockCipherInstance();

  protected BouncyAeadCrypto() { }

  @Override
  public byte[] encrypt(final byte[] input, final CryptoKey key) {
    byte[] nonce = generateNonce();
    AEADCipher cipher = getNewAeadBlockCipherInstance();

    AEADParameters cipherParameters = getParameters(key, nonce);
    try {
      cipher.init(true, cipherParameters);
      int outputLen = cipher.getOutputSize(input.length);
      byte[] output = new byte[outputLen + nonce.length];
      int position = cipher.processBytes(input, 0, input.length, output, 0);

      try {
        cipher.doFinal(output, position);
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during encrypt cipher finalization", e);
      }

      appendNonce(output, nonce);
      return output;
    }
    finally {
      ManagedBufferUtils.wipeByteArray(cipherParameters.getKey().getKey());
    }
  }

  @Override
  public byte[] decrypt(final byte[] input, final CryptoKey key) {
    byte[] nonce = getAppendedNonce(input);
    AEADCipher cipher = getNewAeadBlockCipherInstance();

    AEADParameters cipherParameters = getParameters(key, nonce);
    try {
      cipher.init(false, cipherParameters);
      int cipherTextLength = input.length - nonce.length;
      int outputLen = cipher.getOutputSize(cipherTextLength);
      byte[] output = new byte[outputLen];
      int position = cipher.processBytes(input, 0, cipherTextLength, output, 0);

      try {
        position += cipher.doFinal(output, position);
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during decrypt cipher finalization", e);
      }

      if (position != outputLen) {
        if (Debug.ON) {
          LOG.error("position {} not equal to outputLength {}", position, outputLen);
        }
        throw new AppEncryptionException("position not equal to outputLength");
      }

      return output;
    }
    finally {
      ManagedBufferUtils.wipeByteArray(cipherParameters.getKey().getKey());
    }
  }

  AEADParameters getParameters(final CryptoKey key, final byte[] nonce) {
    return key.withKey(keyBytes -> {
      return new AEADParameters(new KeyParameter(keyBytes), MacSizeBits, nonce);
    });
  }

  protected int getNonceSizeBits() {
    return NonceSizeBits;
  }

  protected int getKeySizeBits() {
    return KeySizeBits;
  }

  protected int getMacSizeBits() {
    return MacSizeBits;
  }
}

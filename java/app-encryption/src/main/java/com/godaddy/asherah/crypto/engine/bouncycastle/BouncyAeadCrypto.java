package com.godaddy.asherah.crypto.engine.bouncycastle;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Arrays;

import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.securememory.Debug;

public abstract class BouncyAeadCrypto extends AeadEnvelopeCrypto {
  private static final Logger LOG = LoggerFactory.getLogger(BouncyAeadCrypto.class);

  //Do NOT modify the size of the nonce unless you've read NIST 800-38D
  private static final int NonceSizeBits = 96;
  private static final int MacSizeBits = 128;
  private static final int KeySizeBits = 256;
  private static final int BufferSize = 4096; // TODO Use default buffer size

  protected abstract AEADCipher getNewAeadCipherInstance();

  protected BouncyAeadCrypto() {
  }

  @Override
  public byte[] encrypt(final byte[] input, final CryptoKey key) {
    byte[] nonce = generateNonce();
    AEADCipher cipher = getNewAeadCipherInstance();

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
    AEADCipher cipher = getNewAeadCipherInstance();

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

  @Override
  public void encryptStream(final InputStream inputStream, final OutputStream outputStream,
                            final CryptoKey key) throws IOException {
    byte[] buf = new byte[BufferSize];
    int data = inputStream.read(buf);
    while (data != -1) {
      byte[] nonce = generateNonce();
      AEADCipher cipher = getNewAeadCipherInstance();
      AEADParameters cipherParameters = getParameters(key, nonce);
      cipher.init(true, cipherParameters);
      int outputLen = cipher.getOutputSize(buf.length);
      byte[] output = new byte[outputLen + nonce.length];
      int position = cipher.processBytes(buf, 0, buf.length, output, 0);
      try {
        cipher.doFinal(output, position);
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during encrypt cipher finalization", e);
      }
      appendNonce(output, nonce);
      outputStream.write(output);
      cipher.reset();
      data = inputStream.read(buf);
    }
  }

  @Override
  public void decryptStream(final InputStream inputStream, final OutputStream outputStream,
                            final CryptoKey key) throws IOException {
    int nonceLength = getNonceSizeBits() / Byte.SIZE;
    byte[] buf = new byte[BufferSize + getMacSizeBits() / Byte.SIZE + nonceLength];
    int data = inputStream.read(buf);
    while (data != -1) {
      byte[] nonce = getAppendedNonce(buf);
      AEADCipher cipher = getNewAeadCipherInstance();
      AEADParameters cipherParameters = getParameters(key, nonce);
      cipher.init(false, cipherParameters);
      int cipherTextLength = buf.length - nonce.length;
      int outputLen = cipher.getOutputSize(cipherTextLength);
      byte[] output = new byte[outputLen];
      int position = cipher.processBytes(buf, 0, cipherTextLength, output, 0);
      try {
        cipher.doFinal(output, position);
      }
      catch (Exception e) {
        throw new AppEncryptionException("unexpected error during encrypt cipher finalization", e);
      }
      outputStream.write(trim(output));
      cipher.reset();
      data = inputStream.read(buf);
    }
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

  private byte[] trim(final byte[] bytes) {
    int i = bytes.length - 1;
    while (i >= 0 && bytes[i] == 0) {
      --i;
    }
    return Arrays.copyOf(bytes, i + 1);
  }
}

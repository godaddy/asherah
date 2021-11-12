package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.CipherParameters;
import org.bouncycastle.crypto.StreamCipher;
import org.bouncycastle.crypto.modes.AEADBlockCipher;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.securememory.Debug;

public abstract class BouncyStreamCrypto extends AeadEnvelopeCrypto {
  private static final Logger LOG = LoggerFactory.getLogger(BouncyStreamCrypto.class);

  protected abstract StreamCipher getNewAeadBlockCipherInstance();

  protected abstract CipherParameters getParameters(CryptoKey key, byte[] nonce);

  protected BouncyStreamCrypto() { }

  @Override
  public byte[] encrypt(final byte[] input, final CryptoKey key) {
    StreamCipher cipher = getNewAeadBlockCipherInstance();
    KeyParameter keyParameter = key.withKey(keyBytes -> (new KeyParameter(keyBytes)));
    try {

      cipher.init(true, keyParameter);
      byte[] output = new byte[input.length];
      for(int i=0;i< input.length;i++) {
        output[i] = cipher.returnByte(input[i]);
      }
      return output;
    }
    finally {
      ManagedBufferUtils.wipeByteArray(keyParameter.getKey());
    }
  }

  @Override
  public byte[] decrypt(final byte[] input, final CryptoKey key) {
    StreamCipher cipher = getNewAeadBlockCipherInstance();

    KeyParameter keyParameter = key.withKey(keyBytes -> (new KeyParameter(keyBytes)));
    try {
      cipher.init(false, keyParameter);
      byte[] output = new byte[input.length];
      cipher.processBytes(input, 0, input.length, output, 0);

      return output;
    }
    finally {
      ManagedBufferUtils.wipeByteArray(keyParameter.getKey());
    }
  }
}

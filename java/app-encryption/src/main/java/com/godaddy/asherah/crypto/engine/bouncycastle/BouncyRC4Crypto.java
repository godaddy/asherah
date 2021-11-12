package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.StreamCipher;
import org.bouncycastle.crypto.engines.AESEngine;
import org.bouncycastle.crypto.engines.RC4Engine;
import org.bouncycastle.crypto.modes.AEADBlockCipher;
import org.bouncycastle.crypto.modes.GCMBlockCipher;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;

import com.godaddy.asherah.crypto.keys.CryptoKey;

public class BouncyRC4Crypto extends BouncyStreamCrypto {

  //Do NOT modify the size of the nonce unless you've read NIST 800-38D
  private static final int NonceSizeBits = 96;
  private static final int MacSizeBits = 128;
  private static final int KeySizeBits = 256;

  /**
   * Creates a new {@link BouncyAeadCrypto} instance.
   */
  public BouncyRC4Crypto() {
    super();
  }

  @Override
  protected StreamCipher getNewAeadBlockCipherInstance() {
    return new RC4Engine();
  }

  @Override
  protected AEADParameters getParameters(final CryptoKey key, final byte[] nonce) {
    return key.withKey(keyBytes -> {
      return new AEADParameters(new KeyParameter(keyBytes), MacSizeBits, nonce);
    });
  }

  @Override
  protected int getNonceSizeBits() {
    return NonceSizeBits;
  }

  @Override
  protected int getKeySizeBits() {
    return KeySizeBits;
  }

  @Override
  protected int getMacSizeBits() {
    return MacSizeBits;
  }

}

package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.engines.AESEngine;
import org.bouncycastle.crypto.modes.AEADBlockCipher;
import org.bouncycastle.crypto.modes.GCMBlockCipher;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;

import com.godaddy.asherah.crypto.keys.CryptoKey;

public class BouncyAes256GcmCrypto extends BouncyAeadCrypto {

  //Do NOT modify the size of the nonce unless you've read NIST 800-38D
  private static final int NonceSizeBits = 96;
  private static final int MacSizeBits = 128;
  private static final int KeySizeBits = 256;

  /**
   * Creates a new {@link com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAeadCrypto} instance.
   */
  public BouncyAes256GcmCrypto() {
    super();
  }

  @Override
  protected AEADBlockCipher getNewAeadBlockCipherInstance() {
    return GCMBlockCipher.newInstance(AESEngine.newInstance());
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

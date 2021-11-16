package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.StreamCipher;
import org.bouncycastle.crypto.macs.SipHash;
import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.modes.ChaCha20Poly1305;
import org.bouncycastle.crypto.params.AEADParameters;
import org.bouncycastle.crypto.params.KeyParameter;

import com.godaddy.asherah.crypto.keys.CryptoKey;

public class BouncyChaCha20Poly1305Crypto extends BouncyAeadCrypto {

  //Do NOT modify the size of the nonce unless you've read NIST 800-38D
  private static final int NonceSizeBits = 96;
  private static final int MacSizeBits = 128;
  private static final int KeySizeBits = 256;

  /**
   * Creates a new {@link BouncyAeadCrypto} instance.
   */
  public BouncyChaCha20Poly1305Crypto() {
    super();
  }

  @Override
  protected AEADCipher getNewAeadBlockCipherInstance() {
    return new ChaCha20Poly1305();
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

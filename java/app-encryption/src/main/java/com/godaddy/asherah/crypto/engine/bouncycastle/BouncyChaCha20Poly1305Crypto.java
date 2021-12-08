package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.modes.ChaCha20Poly1305;

public class BouncyChaCha20Poly1305Crypto extends BouncyAeadCrypto {

  /**
   * Creates a new {@link BouncyAeadCrypto} instance.
   */
  public BouncyChaCha20Poly1305Crypto() {
    super();
  }

  @Override
  protected AEADCipher getNewAeadCipherInstance() {
    return new ChaCha20Poly1305();
  }
}

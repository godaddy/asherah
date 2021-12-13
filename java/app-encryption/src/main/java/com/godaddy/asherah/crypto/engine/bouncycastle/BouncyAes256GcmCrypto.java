package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.engines.AESEngine;
import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.modes.GCMBlockCipher;

public class BouncyAes256GcmCrypto extends BouncyAeadCrypto {

  /**
   * Creates a new {@link com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAeadCrypto} instance.
   */
  public BouncyAes256GcmCrypto() {
    super();
  }

  @Override
  protected AEADCipher getNewAeadCipherInstance() {
    return new GCMBlockCipher(new AESEngine());
  }
}

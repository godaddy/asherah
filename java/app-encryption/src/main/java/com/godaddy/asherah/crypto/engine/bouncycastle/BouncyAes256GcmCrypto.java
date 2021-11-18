package com.godaddy.asherah.crypto.engine.bouncycastle;

import org.bouncycastle.crypto.engines.AESEngine;
import org.bouncycastle.crypto.modes.AEADBlockCipher;
import org.bouncycastle.crypto.modes.GCMBlockCipher;

public class BouncyAes256GcmCrypto extends BouncyAeadCrypto {

  /**
   * Creates a new {@link com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAeadCrypto} instance.
   */
  public BouncyAes256GcmCrypto() {
    super();
  }

  @Override
  protected AEADBlockCipher getNewAeadBlockCipherInstance() {
    return new GCMBlockCipher(new AESEngine());
  }
}

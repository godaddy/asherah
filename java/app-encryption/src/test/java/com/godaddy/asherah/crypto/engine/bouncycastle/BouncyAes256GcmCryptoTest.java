package com.godaddy.asherah.crypto.engine.bouncycastle;

import com.godaddy.asherah.crypto.engine.GenericAeadCryptoTest;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;

class BouncyAes256GcmCryptoTest extends GenericAeadCryptoTest {
  @Override
  protected AeadEnvelopeCrypto getCryptoInstance() {
    return new BouncyAes256GcmCrypto();
  }
}

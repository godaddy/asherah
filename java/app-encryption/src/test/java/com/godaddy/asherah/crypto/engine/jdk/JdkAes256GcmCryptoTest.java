package com.godaddy.asherah.crypto.engine.jdk;

import com.godaddy.asherah.crypto.engine.GenericAeadCryptoTest;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;

class JdkAes256GcmCryptoTest extends GenericAeadCryptoTest {
  @Override
  protected AeadEnvelopeCrypto getCryptoInstance() {
    return new JdkAes256GcmCrypto();
  }
}


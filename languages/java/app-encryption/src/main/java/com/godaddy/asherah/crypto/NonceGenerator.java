package com.godaddy.asherah.crypto;

import java.security.SecureRandom;

public class NonceGenerator {
  // TODO Add ThreadLocal wrapper. Maybe consider adding periodic reseeding of this
  private final SecureRandom secureRandom = new SecureRandom();

  public byte[] createNonce(final int bits) {
    if (bits % Byte.SIZE != 0) {
      throw new IllegalArgumentException("Bits parameter must be multiple of 8");
    }

    byte[] keyBytes = new byte[bits / Byte.SIZE];
    secureRandom.nextBytes(keyBytes);
    return keyBytes;
  }
}

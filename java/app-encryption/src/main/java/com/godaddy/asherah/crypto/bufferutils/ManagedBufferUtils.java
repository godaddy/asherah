package com.godaddy.asherah.crypto.bufferutils;

import org.bouncycastle.util.Arrays;

public final class ManagedBufferUtils {
  private ManagedBufferUtils() { }

  public static void wipeByteArray(final byte[] sensitiveData) {
    Arrays.clear(sensitiveData);
  }

}

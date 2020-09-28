package com.godaddy.asherah.crypto.bufferutils;

import org.bouncycastle.util.Arrays;

public final class ManagedBufferUtils {
  private ManagedBufferUtils() { }

  /**
   * Clears the byte array.
   * @param sensitiveData An array of {@code byte} objects that needs to be cleared.
   */
  public static void wipeByteArray(final byte[] sensitiveData) {
    Arrays.clear(sensitiveData);
  }
}

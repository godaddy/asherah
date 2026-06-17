package com.godaddy.asherah.crypto.bufferutils;

import java.util.Arrays;

public final class ManagedBufferUtils {
  private ManagedBufferUtils() { }

  /**
   * Clears the byte array by filling it with zeros.
   * @param sensitiveData An array of {@code byte} objects that needs to be cleared.
   */
  public static void wipeByteArray(final byte[] sensitiveData) {
    if (sensitiveData != null) {
      Arrays.fill(sensitiveData, (byte) 0);
    }
  }
}

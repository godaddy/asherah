package com.godaddy.asherah.crypto.bufferutils;

import org.bouncycastle.util.Arrays;

public final class ManagedBufferUtils {
  private ManagedBufferUtils() { }

  /**
   * Clears the byte array.
   * @param sensitiveData an array of {@link byte} objects.
   */
  public static void wipeByteArray(final byte[] sensitiveData) {
    Arrays.clear(sensitiveData);
  }

}

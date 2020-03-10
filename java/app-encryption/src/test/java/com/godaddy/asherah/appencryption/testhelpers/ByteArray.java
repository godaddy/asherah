package com.godaddy.asherah.appencryption.testhelpers;

public class ByteArray {
  public static Boolean isAllZeros(byte[] input) {
    for (byte b : input) {
      if (b != 0) {
        return false;
      }
    }
    return true;
  }
}

package com.godaddy.asherah.appencryption.utils;

public interface SafeAutoCloseable extends AutoCloseable {
  /**
   * Close the resource.
   */
  void close();
}

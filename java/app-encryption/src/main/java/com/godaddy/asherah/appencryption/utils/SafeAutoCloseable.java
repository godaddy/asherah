package com.godaddy.asherah.appencryption.utils;

public interface SafeAutoCloseable extends AutoCloseable {
  /**
   * {@inheritDoc}
   */
  void close();
}

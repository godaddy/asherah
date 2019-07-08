package com.godaddy.asherah.crypto.keys;

import java.time.Instant;
import java.util.function.Function;
import java.util.function.Consumer;

class SharedCryptoKey extends CryptoKey {
  private final CryptoKey sharedKey;

  SharedCryptoKey(final CryptoKey sharedKey) {
    this.sharedKey = sharedKey;
  }

  CryptoKey getSharedKey() {
    return sharedKey;
  }

  @Override
  public Instant getCreated() {
    return sharedKey.getCreated();
  }

  @Override
  public boolean isRevoked() {
    return sharedKey.isRevoked();
  }

  @Override
  public void markRevoked() {
    sharedKey.markRevoked();
  }

  @Override
  public void withKey(final Consumer<byte[]> action) {
    sharedKey.withKey(action);
  }

  @Override
  public <T> T withKey(final Function<byte[], T> action) {
    return sharedKey.withKey(action);
  }

  @Override
  public void close() {
    //SharedCryptoKey doesn't *own* any secrets so it doesn't have anything to close
  }
}

package com.godaddy.asherah.crypto.keys;

import com.godaddy.asherah.crypto.CryptoPolicy;

public class SecureCryptoKeyMapFactory<T> {
  private final CryptoPolicy cryptoPolicy;

  public SecureCryptoKeyMapFactory(final CryptoPolicy cryptoPolicy) {
    this.cryptoPolicy = cryptoPolicy;
  }

  public SecureCryptoKeyMap<T> createSecureCryptoKeyMap() {
    return new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis());
  }
}

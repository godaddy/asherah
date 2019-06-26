package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMapFactory;

public class FakeSecureCryptoKeyMapFactory<T> extends SecureCryptoKeyMapFactory<T> {
  private final SecureCryptoKeyMap<T> secureCryptoKeyMap;

  public FakeSecureCryptoKeyMapFactory(final SecureCryptoKeyMap<T> secureCryptoKeyMap) {
    super(null);
    this.secureCryptoKeyMap = secureCryptoKeyMap;
  }

  @Override
  public SecureCryptoKeyMap<T> createSecureCryptoKeyMap() {
    return secureCryptoKeyMap;
  }
}

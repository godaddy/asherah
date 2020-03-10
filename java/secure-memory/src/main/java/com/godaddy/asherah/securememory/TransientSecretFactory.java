package com.godaddy.asherah.securememory;

import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecretFactory;

public class TransientSecretFactory implements SecretFactory {
  private final SecretFactory secretFactory;

  public TransientSecretFactory() {
    secretFactory = new ProtectedMemorySecretFactory();
  }

  @Override
  public Secret createSecret(final byte[] secretData) {
    return secretFactory.createSecret(secretData);
  }

  @Override
  public Secret createSecret(final char[] secretData) {
    return secretFactory.createSecret(secretData);
  }
}

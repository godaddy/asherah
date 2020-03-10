package com.godaddy.asherah.securememory;

public interface SecretFactory {
  Secret createSecret(byte[] secretData);

  Secret createSecret(char[] secretData);
}

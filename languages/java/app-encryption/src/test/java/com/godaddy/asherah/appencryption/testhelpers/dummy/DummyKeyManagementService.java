package com.godaddy.asherah.appencryption.testhelpers.dummy;

import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

import java.time.Instant;

public class DummyKeyManagementService implements KeyManagementService {
  private final CryptoKey encryptionKey;
  private final BouncyAes256GcmCrypto crypto = new BouncyAes256GcmCrypto();

  public DummyKeyManagementService() {
    encryptionKey = crypto.generateKey();
  }

  @Override
  public byte[] encryptKey(CryptoKey key) {
    return crypto.encryptKey(key, encryptionKey);
  }

  @Override
  public CryptoKey decryptKey(byte[] keyCipherText, Instant keyCreated, boolean revoked) {
    return crypto.decryptKey(keyCipherText, keyCreated, encryptionKey, revoked);
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[kms_arn=LOCAL, crypto=" + crypto + "]";
  }
}

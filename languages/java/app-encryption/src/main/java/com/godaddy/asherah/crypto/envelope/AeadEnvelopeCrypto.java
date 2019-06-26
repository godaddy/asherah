package com.godaddy.asherah.crypto.envelope;

import static com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils.wipeByteArray;

import java.time.Instant;

import com.godaddy.asherah.crypto.AeadCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

public abstract class AeadEnvelopeCrypto extends AeadCrypto {

  public byte[] encryptKey(final CryptoKey key, final CryptoKey keyEncryptionKey) {
    return key.withKey(keyBytes -> {
      return encrypt(keyBytes, keyEncryptionKey);
    });
  }

  public CryptoKey decryptKey(final byte[] encryptedKey, final Instant encryptedKeyCreated,
      final CryptoKey keyEncryptionKey) {
    return decryptKey(encryptedKey, encryptedKeyCreated, keyEncryptionKey, false);
  }

  public CryptoKey decryptKey(final byte[] encryptedKey, final Instant encryptedKeyCreated,
      final CryptoKey keyEncryptionKey, final boolean revoked) {
    //Invert this into functional
    byte[] decryptedKey = decrypt(encryptedKey, keyEncryptionKey);
    try {
      return generateKeyFromBytes(decryptedKey, encryptedKeyCreated, revoked);
    }
    finally {
      wipeByteArray(decryptedKey);
    }
  }

  public EnvelopeEncryptResult envelopeEncrypt(final byte[] plainText, final CryptoKey keyEncryptionKey) {
    return envelopeEncrypt(plainText, keyEncryptionKey, null);
  }


  public EnvelopeEncryptResult envelopeEncrypt(final byte[] plainText, final CryptoKey keyEncryptionKey,
      final Object userState) {
    try (CryptoKey dataEncryptionKey = generateKey()) {
      EnvelopeEncryptResult result = new EnvelopeEncryptResult();
      result.setCipherText(encrypt(plainText, dataEncryptionKey));
      result.setEncryptedKey(encryptKey(dataEncryptionKey, keyEncryptionKey));
      result.setUserState(userState);
      return result;
    }
  }

  public byte[] envelopeDecrypt(final byte[] cipherText, final byte[] encryptedKey,
      final Instant keyCreated, final CryptoKey keyEncryptionKey) {
    try (CryptoKey plaintextKey = decryptKey(encryptedKey, keyCreated, keyEncryptionKey)) {
      return decrypt(cipherText, plaintextKey);
    }
  }
}

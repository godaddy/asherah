package com.godaddy.asherah.regression;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

import static com.godaddy.asherah.testhelpers.Constants.*;

final class CryptoKeyHolder {
  private static final AeadEnvelopeCrypto CRYPTO = new BouncyAes256GcmCrypto();

  private final CryptoKey systemKey;
  private final CryptoKey intermediateKey;

  CryptoKeyHolder(final CryptoKey systemKey, final CryptoKey intermediateKey) {
    this.systemKey = systemKey;
    this.intermediateKey = intermediateKey;
  }

  static CryptoKeyHolder generateIKSK() {
    // Subtracting 1/2 the KEY_EXPIRY_DAYS so that during the course of testing any new keys
    // can be created with the current timestamp (this avoids conflicts when the test needs
    // to create a new key based on the current timestamp.)
    Instant created = Instant.now()
      .truncatedTo(ChronoUnit.SECONDS)
      .minus(KEY_EXPIRY_DAYS / 2, ChronoUnit.DAYS);
    CryptoKey systemKey = CRYPTO.generateKey(created);
    CryptoKey intermediateKey = CRYPTO.generateKey(created);

    return new CryptoKeyHolder(systemKey, intermediateKey);
  }

  CryptoKey getSystemKey() {
    return systemKey;
  }

  CryptoKey getIntermediateKey() {
    return intermediateKey;
  }
}


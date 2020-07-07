package com.godaddy.asherah.regression;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.testhelpers.KeyState;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;

import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
final class CacheMock {
  private static final AeadEnvelopeCrypto CRYPTO = new BouncyAes256GcmCrypto();

  private final SecureCryptoKeyMap<Instant> intermediateKeyCache;
  private final SecureCryptoKeyMap<Instant> systemKeyCache;

  private CacheMock(final SecureCryptoKeyMap<Instant> systemKeyCache, final SecureCryptoKeyMap<Instant> intermediateKeyCache) {
    this.intermediateKeyCache = intermediateKeyCache;
    this.systemKeyCache = systemKeyCache;
  }


  static CacheMock createCacheMock(final KeyState cacheIK, final KeyState cacheSK,
      final CryptoKeyHolder cryptoKeyHolder) {
    SecureCryptoKeyMap<Instant> systemKeyCacheSpy = spy(new SecureCryptoKeyMap<>(Long.MAX_VALUE / 2));
    SecureCryptoKeyMap<Instant> intermediateKeyCacheSpy = spy(new SecureCryptoKeyMap<>(Long.MAX_VALUE / 2));

    if (cacheSK != KeyState.EMPTY) {
      CryptoKey systemKey = cryptoKeyHolder.getSystemKey();
      if (cacheSK == KeyState.RETIRED) {
        // We create a revoked copy of the same key
        Instant created = systemKey.getCreated();
        systemKey = systemKey.withKey((bytes) -> {
          return CRYPTO.generateKeyFromBytes(bytes, created, true);
        });
      }

      systemKeyCacheSpy.putAndGetUsable(systemKey.getCreated(), systemKey);
    }

    if (cacheIK != KeyState.EMPTY) {
      CryptoKey intermediateKey = cryptoKeyHolder.getIntermediateKey();
      if (cacheIK == KeyState.RETIRED) {
        // We create a revoked copy of the same key
        Instant created = intermediateKey.getCreated();
        intermediateKey = intermediateKey.withKey((bytes) -> {
          return CRYPTO.generateKeyFromBytes(bytes, created, true);
        });
      }

      intermediateKeyCacheSpy.putAndGetUsable(intermediateKey.getCreated(), intermediateKey);
    }

    return new CacheMock(systemKeyCacheSpy, intermediateKeyCacheSpy);
  }

  SecureCryptoKeyMap<Instant> getIntermediateKeyCache() {
    return intermediateKeyCache;
  }

  SecureCryptoKeyMap<Instant> getSystemKeyCache() {
    return systemKeyCache;
  }

}

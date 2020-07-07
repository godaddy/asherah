package com.godaddy.asherah.regression;

import com.godaddy.asherah.appencryption.Partition;
import com.godaddy.asherah.appencryption.envelope.EnvelopeKeyRecord;
import com.godaddy.asherah.appencryption.envelope.KeyMeta;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.testhelpers.KeyState;
import org.json.JSONObject;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;

import static org.mockito.Mockito.*;

@SuppressWarnings("unchecked")
@ExtendWith(MockitoExtension.class)
final class MetastoreMock {
  private static final AeadEnvelopeCrypto CRYPTO = new BouncyAes256GcmCrypto();

  private MetastoreMock() { }

  static Metastore<JSONObject> createMetastoreMock(final Partition partition, final KeyManagementService kms,
      final Metastore<JSONObject> metastore, final KeyState metaIK, final KeyState metaSK,
      final CryptoKeyHolder cryptoKeyHolder) {

    Metastore<JSONObject> metastoreSpy = spy(metastore);
    CryptoKey systemKey = cryptoKeyHolder.getSystemKey();

    if (metaSK != KeyState.EMPTY) {
      if (metaSK == KeyState.RETIRED) {
        // We create a revoked copy of the same key
        Instant created = systemKey.getCreated();
        systemKey = systemKey.withKey((bytes) -> {
          return CRYPTO.generateKeyFromBytes(bytes, created, true);
        });
      }

      EnvelopeKeyRecord systemKeyRecord = new EnvelopeKeyRecord(systemKey.getCreated(),
        null, kms.encryptKey(systemKey), systemKey.isRevoked());
      metastoreSpy.store(partition.getSystemKeyId(), systemKeyRecord.getCreated(),
        systemKeyRecord.toJson());
    }

    if (metaIK != KeyState.EMPTY) {
      CryptoKey intermediateKey = cryptoKeyHolder.getIntermediateKey();
      if (metaIK == KeyState.RETIRED) {
        // We create a revoked copy of the same key
        Instant created = intermediateKey.getCreated();
        intermediateKey = intermediateKey.withKey((bytes) -> {
          return CRYPTO.generateKeyFromBytes(bytes, created, true);
        });
      }

      EnvelopeKeyRecord intermediateKeyRecord = new EnvelopeKeyRecord(intermediateKey.getCreated(),
        new KeyMeta(partition.getSystemKeyId(), systemKey.getCreated()),
        CRYPTO.encryptKey(intermediateKey, systemKey), intermediateKey.isRevoked());
      metastoreSpy.store(partition.getIntermediateKeyId(), intermediateKeyRecord.getCreated(),
        intermediateKeyRecord.toJson());
    }

    reset(metastoreSpy);
    return metastoreSpy;
  }
}


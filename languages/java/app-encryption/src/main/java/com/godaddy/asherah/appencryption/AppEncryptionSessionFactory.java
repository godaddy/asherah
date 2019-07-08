package com.godaddy.asherah.appencryption;

import java.time.Instant;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionBytesImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.keymanagement.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.MemoryPersistenceImpl;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMapFactory;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.config.MeterFilter;

public class AppEncryptionSessionFactory implements SafeAutoCloseable {
  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionSessionFactory.class);

  private final String productId;
  private final String systemId;
  private final MetastorePersistence<JSONObject> metastorePersistence;
  private final SecureCryptoKeyMapFactory<Instant> secureCryptoKeyMapFactory;
  private final SecureCryptoKeyMap<Instant> systemKeyCache;
  private final CryptoPolicy cryptoPolicy;
  private final KeyManagementService keyManagementService;

  public AppEncryptionSessionFactory(
      final String productId,
      final String systemId,
      final MetastorePersistence<JSONObject> metastorePersistence,
      final SecureCryptoKeyMapFactory<Instant> secureCryptoKeyMapFactory,
      final CryptoPolicy cryptoPolicy,
      final KeyManagementService keyManagementService) {
    this.productId = productId;
    this.systemId = systemId;
    this.metastorePersistence = metastorePersistence;
    this.secureCryptoKeyMapFactory = secureCryptoKeyMapFactory;
    this.systemKeyCache = secureCryptoKeyMapFactory.createSecureCryptoKeyMap();
    this.cryptoPolicy = cryptoPolicy;
    this.keyManagementService = keyManagementService;
  }

  public AppEncryption<JSONObject, byte[]> getAppEncryptionJson(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new AppEncryptionJsonImpl<>(envelopeEncryption);
  }

  public AppEncryption<byte[], byte[]> getAppEncryptionBytes(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new AppEncryptionBytesImpl<>(envelopeEncryption);
  }

  public AppEncryption<JSONObject, JSONObject> getAppEncryptionJsonAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new AppEncryptionJsonImpl<>(envelopeEncryption);
  }

  public AppEncryption<byte[], JSONObject> getAppEncryptionBytesAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new AppEncryptionBytesImpl<>(envelopeEncryption);
  }

  EnvelopeEncryption<byte[]> getEnvelopeEncryptionBytes(final String partitionId) {
    return new EnvelopeEncryptionBytesImpl(getEnvelopeEncryptionJson(partitionId));
  }

  private EnvelopeEncryptionJsonImpl getEnvelopeEncryptionJson(final String partitionId) {
    AppEncryptionPartition appEncryptionPartition = getAppEncryptionPartition(partitionId);
    return new EnvelopeEncryptionJsonImpl(
        appEncryptionPartition,
        metastorePersistence,
        systemKeyCache,
        secureCryptoKeyMapFactory,
        new BouncyAes256GcmCrypto(),
        cryptoPolicy,
        keyManagementService);
  }

  AppEncryptionPartition getAppEncryptionPartition(final String partitionId) {
    return new AppEncryptionPartition(partitionId, systemId, productId);
  }

  @Override
  public void close() {
    try {
      // only close system key cache since we invoke its creation
      systemKeyCache.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during close", e);
    }
  }

  public static MetastoreStep newBuilder(final String productId, final String systemId) {
    return new Builder(productId, systemId);
  }

  public static final class Builder implements MetastoreStep, CryptoPolicyStep, KeyManagementServiceStep, BuildStep {
    private final String productId;
    private final String systemId;

    private MetastorePersistence<JSONObject> metastorePersistence;
    private CryptoPolicy cryptoPolicy;
    private KeyManagementService keyManagementService;
    private boolean metricsEnabled = false;

    private Builder(final String productId, final String systemId) {
      this.productId = productId;
      this.systemId = systemId;
    }

    @Override
    public CryptoPolicyStep withMemoryPersistence() {
      this.metastorePersistence = new MemoryPersistenceImpl<>();
      return this;
    }

    @Override
    public CryptoPolicyStep withMetastorePersistence(final MetastorePersistence<JSONObject> persistence) {
      this.metastorePersistence = persistence;
      return this;
    }

    @Override
    public KeyManagementServiceStep withNeverExpiredCryptoPolicy() {
      this.cryptoPolicy = new NeverExpiredCryptoPolicy();
      return this;
    }

    @Override
    public KeyManagementServiceStep withCryptoPolicy(final CryptoPolicy policy) {
      this.cryptoPolicy = policy;
      return this;
    }

    @Override
    public BuildStep withStaticKeyManagementService(final String demoMasterKey) {
      this.keyManagementService = new StaticKeyManagementServiceImpl(demoMasterKey);
      return this;
    }


    @Override
    public BuildStep withKeyManagementService(final KeyManagementService kms) {
      this.keyManagementService = kms;
      return this;
    }

    @Override
    public BuildStep withMetricsEnabled() {
      this.metricsEnabled = true;
      return this;
    }

    @Override
    public AppEncryptionSessionFactory build() {
      if (!metricsEnabled) {
        // Deny takes precedence in the filtering logic, so we deny if they didn't explicitly enable metrics
        Metrics.globalRegistry.config().meterFilter(MeterFilter.denyNameStartsWith(MetricsUtil.AEL_METRICS_PREFIX));
      }

      return new AppEncryptionSessionFactory(productId, systemId, metastorePersistence,
          new SecureCryptoKeyMapFactory<>(cryptoPolicy), cryptoPolicy, keyManagementService);
    }
  }

  public interface MetastoreStep {
    // Leaving this here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
    CryptoPolicyStep withMemoryPersistence();

    CryptoPolicyStep withMetastorePersistence(MetastorePersistence<JSONObject> persistence);
  }

  public interface CryptoPolicyStep {
    KeyManagementServiceStep withNeverExpiredCryptoPolicy();

    KeyManagementServiceStep withCryptoPolicy(CryptoPolicy policy);
  }

  public interface KeyManagementServiceStep {
    // Leaving this here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
    BuildStep withStaticKeyManagementService(String demoMasterKey);

    BuildStep withKeyManagementService(KeyManagementService keyManagementService);
  }

  public interface BuildStep {
    BuildStep withMetricsEnabled();

    AppEncryptionSessionFactory build();
  }
}

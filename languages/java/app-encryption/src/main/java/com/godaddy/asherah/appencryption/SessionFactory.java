package com.godaddy.asherah.appencryption;

import java.time.Instant;
import java.util.concurrent.TimeUnit;

import org.checkerframework.checker.index.qual.NonNegative;
import org.checkerframework.checker.nullness.qual.NonNull;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.github.benmanes.caffeine.cache.Cache;
import com.github.benmanes.caffeine.cache.Caffeine;
import com.github.benmanes.caffeine.cache.Expiry;
import com.github.benmanes.caffeine.cache.RemovalCause;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionBytesImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.config.MeterFilter;

public class SessionFactory implements SafeAutoCloseable {
  private static final Logger logger = LoggerFactory.getLogger(SessionFactory.class);

  private final String productId;
  private final String serviceId;
  private final Metastore<JSONObject> metastore;
  private final SecureCryptoKeyMap<Instant> systemKeyCache;
  private final CryptoPolicy cryptoPolicy;
  private final KeyManagementService keyManagementService;
  private final Cache<String, SecureCryptoKeyMap<Instant>> ikCacheCache;

  public SessionFactory(
      final String productId,
      final String serviceId,
      final Metastore<JSONObject> metastore,
      final SecureCryptoKeyMap<Instant> systemKeyCache,
      final CryptoPolicy cryptoPolicy,
      final KeyManagementService keyManagementService) {
    this.productId = productId;
    this.serviceId = serviceId;
    this.metastore = metastore;
    this.systemKeyCache = systemKeyCache;
    this.cryptoPolicy = cryptoPolicy;
    this.keyManagementService = keyManagementService;

    this.ikCacheCache = Caffeine.newBuilder()
//        .expireAfterAccess(cryptoPolicy.getSharedIkCacheExpireAfterAccessMillis(), TimeUnit.MILLISECONDS)
        .weigher((String intermediateKeyId, SecureCryptoKeyMap<Instant> value) -> (value.isUsed()) ? 0 : 1)
        .maximumWeight(2000)
        .expireAfter(new Expiry<String, SecureCryptoKeyMap<Instant>>() {
          @Override
          public long expireAfterCreate(@NonNull final String key, @NonNull final SecureCryptoKeyMap<Instant> value,
              final long currentTime) {
//            System.out.println("JOEY ENTERED expireAfterCreate");
            return Long.MAX_VALUE;
          }

          @Override
          public long expireAfterRead(@NonNull final String key, @NonNull final SecureCryptoKeyMap<Instant> value,
              final long currentTime, @NonNegative final long currentDuration) {
//            System.out.println("JOEY ENTERED expireAfterRead");
            // No longer in use, so use last used time to calculate when it should expire
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSharedIkCacheExpireAfterAccessMillis());
          }

          @Override
          public long expireAfterUpdate(@NonNull final String key, @NonNull final SecureCryptoKeyMap<Instant> value,
              final long currentTime, @NonNegative final long currentDuration) {
//            System.out.println("JOEY ENTERED expireAfterUpdate");
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSharedIkCacheExpireAfterAccessMillis());
          }
        })
        .removalListener(
            (String intermediateKeyId, SecureCryptoKeyMap<Instant> intermediateKeyCache, RemovalCause cause) -> {
              System.out.println("JOEY removing " + intermediateKeyId + " isUsed = " + intermediateKeyCache.isUsed() + " cause = " + cause);
              intermediateKeyCache.close();
            })
        .build();
//        .build(k -> new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis()));
  }

  SecureCryptoKeyMap<Instant> acquireShared(String key) {
    SecureCryptoKeyMap<Instant> reference = ikCacheCache.asMap().compute(key, (key1, ref)-> {
      if (ref == null) {
        SecureCryptoKeyMap<Instant> newMap = new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis());
        newMap.incrementUsageTracker();
        return newMap;
      }

      ref.incrementUsageTracker();
      return ref;
    });

    return reference;
  }

  void releaseShared(String key) {
    ikCacheCache.asMap().computeIfPresent(key, (key1, ref) -> {
      ref.decrementUsageTracker();
      System.out.println("JOEY still used after decrement = " + ref.isUsed());
      return ref;
    });
  }

  public Session<JSONObject, byte[]> getSessionJson(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new SessionJsonImpl<>(envelopeEncryption);
  }

  public Session<byte[], byte[]> getSessionBytes(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new SessionBytesImpl<>(envelopeEncryption);
  }

  public Session<JSONObject, JSONObject> getSessionJsonAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new SessionJsonImpl<>(envelopeEncryption);
  }

  public Session<byte[], JSONObject> getSessionBytesAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new SessionBytesImpl<>(envelopeEncryption);
  }

  EnvelopeEncryption<byte[]> getEnvelopeEncryptionBytes(final String partitionId) {
    return new EnvelopeEncryptionBytesImpl(getEnvelopeEncryptionJson(partitionId));
  }

  private EnvelopeEncryptionJsonImpl getEnvelopeEncryptionJson(final String partitionId) {
    Partition partition = getPartition(partitionId);

    SecureCryptoKeyMap<Instant> ikCache;
    if (cryptoPolicy.useSharedIntermediateKeyCache()) {
      ikCache = acquireShared(partition.getIntermediateKeyId());
      // Need to track our number of concurrent users to reliably close without consequences
//      ikCache.incrementUsageTracker();
    }
    else {
      ikCache = new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis());
    }

    return new EnvelopeEncryptionJsonImpl(
        partition,
        metastore,
        systemKeyCache,
        ikCache,
        new BouncyAes256GcmCrypto(),
        cryptoPolicy,
        keyManagementService,
        key -> releaseShared(key));
  }

  Partition getPartition(final String partitionId) {
    return new Partition(partitionId, serviceId, productId);
  }

  @Override
  public void close() {
    try {
      systemKeyCache.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during skCache close", e);
    }

    // This should force everything to be evicted and process the cleanup
    ikCacheCache.invalidateAll();
    ikCacheCache.cleanUp();
  }

  public static MetastoreStep newBuilder(final String productId, final String serviceId) {
    return new Builder(productId, serviceId);
  }

  public static final class Builder implements MetastoreStep, CryptoPolicyStep, KeyManagementServiceStep, BuildStep {
    private final String productId;
    private final String serviceId;

    private Metastore<JSONObject> metastore;
    private CryptoPolicy cryptoPolicy;
    private KeyManagementService keyManagementService;
    private boolean metricsEnabled = false;

    private Builder(final String productId, final String serviceId) {
      this.productId = productId;
      this.serviceId = serviceId;
    }

    @Override
    public CryptoPolicyStep withInMemoryMetastore() {
      this.metastore = new InMemoryMetastoreImpl<>();
      return this;
    }

    @Override
    public CryptoPolicyStep withMetastore(final Metastore<JSONObject> metastoreObject) {
      this.metastore = metastoreObject;
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
    public SessionFactory build() {
      if (!metricsEnabled) {
        // Deny takes precedence in the filtering logic, so we deny if they didn't explicitly enable metrics
        Metrics.globalRegistry.config().meterFilter(MeterFilter.denyNameStartsWith(MetricsUtil.AEL_METRICS_PREFIX));
      }

      return new SessionFactory(productId, serviceId, metastore,
          new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis()), cryptoPolicy, keyManagementService);
    }
  }

  public interface MetastoreStep {
    // Leaving this here for now for user integration test convenience. Need to add "don't run in prod" checks somehow
    CryptoPolicyStep withInMemoryMetastore();

    CryptoPolicyStep withMetastore(Metastore<JSONObject> metastore);
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

    SessionFactory build();
  }
}

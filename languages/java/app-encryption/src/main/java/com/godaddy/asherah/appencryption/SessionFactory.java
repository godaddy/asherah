package com.godaddy.asherah.appencryption;

import java.time.Instant;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.function.Function;

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
  private final Cache<String, CachedEnvelopeEncryptionJsonImpl> sessionCache;

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

    this.sessionCache = Caffeine.newBuilder()
        .weigher((String intermediateKeyId, CachedEnvelopeEncryptionJsonImpl session) -> {
          if (session.isUsed()) {
            return 0;
          }
          return 1;
        })
        .maximumWeight(cryptoPolicy.getSessionCacheMaxSize())
        .expireAfter(new Expiry<String, CachedEnvelopeEncryptionJsonImpl>() {
          @Override
          public long expireAfterCreate(@NonNull final String key,
              @NonNull final CachedEnvelopeEncryptionJsonImpl value, final long currentTime) {
            // Always pin on create since we known it will be used
            return Long.MAX_VALUE;
          }

          @Override
          public long expireAfterRead(@NonNull final String key, @NonNull final CachedEnvelopeEncryptionJsonImpl value,
              final long currentTime, @NonNegative final long currentDuration) {
            // If we know it's still in use, don't expire it yet
            if (value.isUsed()) {
              return Long.MAX_VALUE;
            }

            // No longer in use, so now kickoff the expire timer
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSessionCacheExpireMillis());
          }

          @Override
          public long expireAfterUpdate(@NonNull final String key,
              @NonNull final CachedEnvelopeEncryptionJsonImpl value, final long currentTime,
              @NonNegative final long currentDuration) {
            // If we know it's still in use, don't expire it yet
            if (value.isUsed()) {
              return Long.MAX_VALUE;
            }

            // No longer in use, so now kickoff the expire timer
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSessionCacheExpireMillis());
          }
        })
        .removalListener(
            (String key, CachedEnvelopeEncryptionJsonImpl session, RemovalCause cause) -> {
              // actually close the real thing
              session.getEnvelopeEncryptionJsonImpl().close();
            })
        .build();
  }

  CachedEnvelopeEncryptionJsonImpl acquireShared(final Function<String, EnvelopeEncryptionJsonImpl> createSession,
      final String partitionId) {
    return sessionCache.asMap().compute(partitionId, (k, cachedSession) -> {
      if (cachedSession == null) {
        // Creating for first time and increment usage counter as we're the first user
        CachedEnvelopeEncryptionJsonImpl newSession =
            new CachedEnvelopeEncryptionJsonImpl(createSession.apply(partitionId), partitionId);
        newSession.incrementUsageTracker();

        return newSession;
      }

      // Already exists in cache, so just increment usage counter
      cachedSession.incrementUsageTracker();
      return cachedSession;
    });
  }

  void releaseShared(final String partitionId) {
    // Decrements the usage counter if still in the cache
    sessionCache.asMap().computeIfPresent(partitionId, (k, cachedSession) -> {
      cachedSession.decrementUsageTracker();
      return cachedSession;
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

  private EnvelopeEncryption<JSONObject> getEnvelopeEncryptionJson(final String partitionId) {
    Function<String, EnvelopeEncryptionJsonImpl> createFunc = id -> {
      Partition partition = getPartition(partitionId);

      return new EnvelopeEncryptionJsonImpl(
          partition,
          metastore,
          systemKeyCache,
          new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis()),
          new BouncyAes256GcmCrypto(),
          cryptoPolicy,
          keyManagementService);
    };

    if (cryptoPolicy.canCacheSessions()) {
      return acquireShared(createFunc, partitionId);
    }

    return createFunc.apply(partitionId);
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
    sessionCache.invalidateAll();
    sessionCache.cleanUp();
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

  class CachedEnvelopeEncryptionJsonImpl implements EnvelopeEncryption<JSONObject> {
    private final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;
    // The usageCounter is used to determine if any callers are still using this instance.
    private final LongAdder usageCounter = new LongAdder();
    private final String key;

    CachedEnvelopeEncryptionJsonImpl(final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl, final String key) {
      this.envelopeEncryptionJsonImpl = envelopeEncryptionJsonImpl;
      this.key = key;
    }

    @Override
    public JSONObject encryptPayload(final byte[] payload) {
      return envelopeEncryptionJsonImpl.encryptPayload(payload);
    }

    @Override
    public byte[] decryptDataRowRecord(final JSONObject dataRowRecord) {
      return envelopeEncryptionJsonImpl.decryptDataRowRecord(dataRowRecord);
    }

    @Override
    public void close() {
      releaseShared(key);
    }

    EnvelopeEncryptionJsonImpl getEnvelopeEncryptionJsonImpl() {
      return envelopeEncryptionJsonImpl;
    }

    void incrementUsageTracker() {
      usageCounter.increment();
    }

    void decrementUsageTracker() {
      usageCounter.decrement();
    }

    boolean isUsed() {
      return usageCounter.sum() > 0;
    }
  }
}

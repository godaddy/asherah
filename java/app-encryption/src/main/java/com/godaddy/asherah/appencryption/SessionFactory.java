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
import com.google.common.annotations.VisibleForTesting;

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
  private final Cache<String, CachedSession> sessionCache;

  /**
   * Creates a new {@code SessionFactory} instance using the provided parameters. A session factory is required to
   * generate cryptographic sessions.
   *
   * @param productId A unique identifier for a product.
   * @param serviceId A unique identifier for a service.
   * @param metastore A {@link Metastore} implementation used to store system and intermediate keys.
   * @param systemKeyCache A {@link java.util.concurrent.ConcurrentSkipListMap} based implementation for caching
   *                       system keys.
   * @param cryptoPolicy A {@link CryptoPolicy} implementation that dictates the various behaviors of Asherah.
   * @param keyManagementService A {@link KeyManagementService} implementation that generates the top level master key
   *                             and encrypts the system keys using the master key.
   */
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
        .weigher((String intermediateKeyId, CachedSession session) -> {
          // Invoked immediately before any expireXXX calls

          // If a session is still in use, give it a weight of 0 to prevent it from being removed
          if (session.isUsed()) {
            return 0;
          }

          // Session no longer in use, so give regular weight of 1 (using weight as number of entries)
          return 1;
        })
        .maximumWeight(cryptoPolicy.getSessionCacheMaxSize())
        .expireAfter(new Expiry<String, CachedSession>() {
          @Override
          public long expireAfterCreate(@NonNull final String key, @NonNull final CachedSession value,
              final long currentTime) {
            // Invoked when acquireShared is called for new entry, after the compute mapping call completes

            // Always pin on create since we known it will be used
            return Long.MAX_VALUE;
          }

          @Override
          public long expireAfterRead(@NonNull final String key, @NonNull final CachedSession value,
              final long currentTime, @NonNegative final long currentDuration) {
            // Invoked when acquireShared is called for existing entry and the entry is already in use,
            // and when releaseShared is called when an entry is still in use

            // If we know it's still in use, don't expire it yet
            if (value.isUsed()) {
              return Long.MAX_VALUE;
            }

            // No longer in use, so now kickoff the expire timer.
            // May not be reachable since we always use compute-based calls, but good to leave in place anyway
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSessionCacheExpireMillis());
          }

          @Override
          public long expireAfterUpdate(@NonNull final String key, @NonNull final CachedSession value,
              final long currentTime, @NonNegative final long currentDuration) {
            // Invoked when acquireShared is called for existing entry, after compute mapping call completes,
            // and when releaseShared is called, after computeIfPresent mapping call completes.

            // If we know it's still in use, don't expire it yet
            if (value.isUsed()) {
              return Long.MAX_VALUE;
            }

            // No longer in use, so now kickoff the expire timer
            return TimeUnit.MILLISECONDS.toNanos(cryptoPolicy.getSessionCacheExpireMillis());
          }
        })
        .removalListener((String key, CachedSession session, RemovalCause cause) -> {
          // Since evictions are delayed/amortized as part of other operations, this may be invoked while a caller
          // is attempting to access this session after it already expired. In that scenario, the caller will
          // go through the create entry flow while this current instance is safely closed.

          // actually close the real thing
          session.getEnvelopeEncryptionJsonImpl().close();
        })
        .build();
  }

  @VisibleForTesting
  Cache<String, CachedSession> getSessionCache() {
    return sessionCache;
  }

  /**
   * Atomically acquires a shared {@code CachedSession} from the session cache for the {@code partitionId}, creating
   * a new one using the given function if needed. This is used to track the number of concurrent users so cache
   * eviction policies don't remove an entry while it's still potentially in use.
   *
   * @param createSession The function to create a new session if there is no current mapping.
   * @param partitionId A unique identifier for a session.
   * @return The cached session that's mapped for the given {@code partitionId}.
   */
  CachedSession acquireShared(final Function<String, EnvelopeEncryptionJsonImpl> createSession,
      final String partitionId) {
    return sessionCache.asMap().compute(partitionId, (k, cachedSession) -> {
      if (cachedSession == null) {
        // Creating for first time and increment usage counter as we're the first user
        CachedSession newSession = new CachedSession(createSession.apply(partitionId), partitionId);
        newSession.incrementUsageTracker();

        return newSession;
      }

      // Already exists in cache, so just increment usage counter
      cachedSession.incrementUsageTracker();
      return cachedSession;
    });
  }

  /**
   * Atomically marks a shared {@code CachedSession} in the session cache as no longer being used by the current
   * caller for the {@code partitionId}. This is used to track the number of concurrent users so cache eviction
   * policies don't remove an entry while it's still potentially in use.
   *
   * @param partitionId A unique identifier for a session.
   */
  void releaseShared(final String partitionId) {
    // Decrements the usage counter if still in the cache
    sessionCache.asMap().computeIfPresent(partitionId, (k, cachedSession) -> {
      cachedSession.decrementUsageTracker();
      return cachedSession;
    });
  }

  /**
   * Uses the {@code partitionId} to get an {@link EnvelopeEncryptionBytesImpl} instance.
   *
   * @param partitionId A unique identifier for a session.
   * @return A {@link Session} that encrypts a json payload and stores it as byte[].
   */
  public Session<JSONObject, byte[]> getSessionJson(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new SessionJsonImpl<>(envelopeEncryption);
  }

  /**
   * Uses the {@code partitionId} to get the {@link EnvelopeEncryptionJsonImpl} instance.
   *
   * @param partitionId A unique identifier for a session.
   * @return A {@link Session} that encrypts a byte[] payload and stores it as byte[].
   */
  public Session<byte[], byte[]> getSessionBytes(final String partitionId) {
    EnvelopeEncryption<byte[]> envelopeEncryption = getEnvelopeEncryptionBytes(partitionId);

    return new SessionBytesImpl<>(envelopeEncryption);
  }

  /**
   * Uses the {@code partitionId} to get the {@link EnvelopeEncryptionJsonImpl} instance.
   *
   * @param partitionId A unique identifier for a session.
   * @return A {@link Session} that encrypts a json payload and stores it as json.
   */
  public Session<JSONObject, JSONObject> getSessionJsonAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new SessionJsonImpl<>(envelopeEncryption);
  }

  /**
   * Uses the {@code partitionId} to get the {@link EnvelopeEncryptionJsonImpl} instance.
   *
   * @param partitionId A unique identifier for a session.
   * @return A {@link Session} that encrypts a byte[] payload and stores it as json.
   */
  public Session<byte[], JSONObject> getSessionBytesAsJson(final String partitionId) {
    EnvelopeEncryption<JSONObject> envelopeEncryption = getEnvelopeEncryptionJson(partitionId);

    return new SessionBytesImpl<>(envelopeEncryption);
  }

  EnvelopeEncryption<byte[]> getEnvelopeEncryptionBytes(final String partitionId) {
    return new EnvelopeEncryptionBytesImpl(getEnvelopeEncryptionJson(partitionId));
  }

  private EnvelopeEncryption<JSONObject> getEnvelopeEncryptionJson(final String partitionId) {
    // Wrap the creation logic in a lambda so the cache entry acquisition can create a new instance when needed
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

  /**
   * Initialize a session factory builder.
   *
   * @param productId A unique identifier for a product, used to create a {@code SessionFactory} object.
   * @param serviceId A unique identifier for a service, used to create a {@code SessionFactory} object.
   * @return The current {@code MetastoreStep} instance with initialized {@code productId} and {@code serviceId}.
   */
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
    public BuildStep withStaticKeyManagementService(final String staticMasterKey) {
      this.keyManagementService = new StaticKeyManagementServiceImpl(staticMasterKey);
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
    /**
     * Initialize a session factory builder step with an {@link InMemoryMetastoreImpl} object.
     * NOTE: This is for user integration test convenience. Need to add "don't run in prod" checks!
     *
     * @return The current {@code CryptoPolicyStep} instance initialized with an {@link InMemoryMetastoreImpl} object.
     */
    CryptoPolicyStep withInMemoryMetastore();

    /**
     * Initialize a session factory builder step with the provided metastore.
     *
     * @param metastore The {@link Metastore} implementation to use.
     * @return The current {@code CryptoPolicyStep} instance initialized with some {@link Metastore} implementation.
     */
    CryptoPolicyStep withMetastore(Metastore<JSONObject> metastore);
  }

  public interface CryptoPolicyStep {
    /**
     * Initialize a session factory builder step with a new {@link NeverExpiredCryptoPolicy} object.
     *
     * @return The current {@code KeyManagementServiceStep} instance initialized with a {@link NeverExpiredCryptoPolicy}
     *         object.
     */
    KeyManagementServiceStep withNeverExpiredCryptoPolicy();

    /**
     * Initialize a session factory builder step with the provided crypto policy.
     *
     * @param policy The {@link CryptoPolicy} implementation to use.
     * @return The current {@code KeyManagementServiceStep} instance initialized with some {@link CryptoPolicy}
     *         implementation.
     */
    KeyManagementServiceStep withCryptoPolicy(CryptoPolicy policy);
  }

  public interface KeyManagementServiceStep {
    /**
     * Initialize a session factory builder step with a new {@link StaticKeyManagementServiceImpl} object.
     * NOTE: This is for user integration test convenience. Need to add "don't run in prod" checks!
     *
     * @param staticMasterKey The static key.
     * @return The current {@code BuildStep} instance initialized with a {@link StaticKeyManagementServiceImpl} object.
     */
    BuildStep withStaticKeyManagementService(String staticMasterKey);

    /**
     * Initialize a session factory builder step with the provided key management service.
     *
     * @param keyManagementService the {@link KeyManagementService} implementation to use.
     * @return The current {@code BuildStep} instance initialized with some {@link KeyManagementService} implementation.
     */
    BuildStep withKeyManagementService(KeyManagementService keyManagementService);
  }

  public interface BuildStep {
    /**
     * Enable metrics for the {@code SessionFactory}.
     *
     * @return The current {@code BuildStep} instance with metrics enabled.
     */
    BuildStep withMetricsEnabled();

    /**
     * Builds the finalized session factory with the parameters specified in the {@code Builder}.
     *
     * @return The fully instantiated {@code SessionFactory}.
     */
    SessionFactory build();
  }

  // Calling it CachedSession but we actually cache the implementing envelope encryption class. Tried
  // to cache Session instances but the generics and current implementation made it overly difficult to do so.
  class CachedSession implements EnvelopeEncryption<JSONObject> {
    private final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;
    // The usageCounter is used to determine if any callers are still using this instance.
    private final LongAdder usageCounter = new LongAdder();
    private final String key;

    CachedSession(final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl, final String key) {
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
      // Instead of closing the session, we call the release function so it can atomically update the usage counter
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

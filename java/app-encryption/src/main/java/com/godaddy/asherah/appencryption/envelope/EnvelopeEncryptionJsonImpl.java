package com.godaddy.asherah.appencryption.envelope;

import com.godaddy.asherah.appencryption.Partition;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.exceptions.MetadataMissingException;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.appencryption.utils.Json;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.envelope.EnvelopeEncryptResult;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.Timer;

import java.time.Instant;
import java.util.Optional;
import java.util.function.Function;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class EnvelopeEncryptionJsonImpl implements EnvelopeEncryption<JSONObject> {
  private static final Logger logger = LoggerFactory.getLogger(EnvelopeEncryptionJsonImpl.class);

  private final Timer encryptTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".drr.encrypt");
  private final Timer decryptTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".drr.decrypt");

  private final Partition partition;
  private final Metastore<JSONObject> metastore;
  private final SecureCryptoKeyMap<Instant> systemKeyCache; // note assumed being limited to 1 product/service id pair
  private final SecureCryptoKeyMap<Instant> intermediateKeyCache;
  private final AeadEnvelopeCrypto crypto;
  private final CryptoPolicy cryptoPolicy;
  private final KeyManagementService keyManagementService;

  /**
   * Creates a new {@code EnvelopeEncryptionBytesImpl} instance using the provided parameters. This is an
   * implementation of {@link EnvelopeEncryption} which uses {@link org.json.JSONObject} as the Data Row Record format.
   *
   * @param partition A {@link Partition} object.
   * @param metastore A {@link Metastore} implementation used to store system and intermediate keys.
   * @param systemKeyCache A {@link java.util.concurrent.ConcurrentSkipListMap} based implementation for caching
   *                       system keys.
   * @param intermediateKeyCache A {@link java.util.concurrent.ConcurrentSkipListMap} based implementation for caching
   *                       intermediate keys.
   * @param aeadEnvelopeCrypto An implementation of {@link AeadEnvelopeCrypto}, used to encrypt/decrypt keys and
   *                           envelopes (payload and the key).
   * @param cryptoPolicy A {@link CryptoPolicy} implementation that dictates the various behaviors of Asherah.
   * @param keyManagementService A {@link KeyManagementService} implementation that generates the top level master key
   *                             and encrypts the system keys.
   */
  public EnvelopeEncryptionJsonImpl(final Partition partition,
      final Metastore<JSONObject> metastore, final SecureCryptoKeyMap<Instant> systemKeyCache,
      final SecureCryptoKeyMap<Instant> intermediateKeyCache, final AeadEnvelopeCrypto aeadEnvelopeCrypto,
      final CryptoPolicy cryptoPolicy, final KeyManagementService keyManagementService) {
    this.partition = partition;
    this.metastore = metastore;
    this.systemKeyCache = systemKeyCache;
    this.intermediateKeyCache = intermediateKeyCache;
    this.crypto = aeadEnvelopeCrypto;
    this.cryptoPolicy = cryptoPolicy;
    this.keyManagementService = keyManagementService;
  }

  @Override
  public byte[] decryptDataRowRecord(final JSONObject dataRowRecord) {
    return decryptTimer.record(() -> {
      Json dataRowRecordJson = new Json(dataRowRecord);

      Json keyDocument = dataRowRecordJson.getJson("Key");
      byte[] payloadEncrypted = dataRowRecordJson.getBytes("Data");

      EnvelopeKeyRecord dataRowKeyRecord = new EnvelopeKeyRecord(keyDocument);

      byte[] decryptedPayload = withIntermediateKeyForRead(
          dataRowKeyRecord.getParentKeyMeta().orElseThrow(
              () -> new MetadataMissingException("Could not find parentKeyMeta (IK) for dataRowKey")
          ),
          (intermediateCryptoKey) -> crypto.envelopeDecrypt(payloadEncrypted, dataRowKeyRecord.getEncryptedKey(),
              dataRowKeyRecord.getCreated(), intermediateCryptoKey));

      return decryptedPayload;
    });
  }

  @Override
  public JSONObject encryptPayload(final byte[] payload) {
    return encryptTimer.record(() -> {
      EnvelopeEncryptResult result = withIntermediateKeyForWrite((intermediateCryptoKey) ->
          crypto.envelopeEncrypt(payload, intermediateCryptoKey,
            new KeyMeta(partition.getIntermediateKeyId(), intermediateCryptoKey.getCreated()))
      );
      KeyMeta parentKeyMeta = (KeyMeta) result.getUserState();

      EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(Instant.now(), parentKeyMeta, result.getEncryptedKey());

      Json wrapperDocument = new Json();
      wrapperDocument.put("Key", keyRecord.toJson());
      wrapperDocument.put("Data", result.getCipherText());

      return wrapperDocument.toJsonObject();
    });
  }

  @Override
  public void close() {
    try {
      // only close intermediate key cache since its lifecycle is tied to this "session"
      intermediateKeyCache.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during close", e);
    }
  }

  /**
   * Calls a function using a decrypted intermediate key that was previously used.
   *
   * @param intermediateKeyMeta The {@code IntermediateKey} meta used previously to write a DRR.
   * @param functionWithIntermediateKey The function to call using the decrypted intermediate key.
   * @return The result returned by the {@code functionWithIntermediateKey}.
   * @throws MetadataMissingException If the intermediate key is not found, or it has missing system key info.
   */
  <T> T withIntermediateKeyForRead(final KeyMeta intermediateKeyMeta,
      final Function<CryptoKey, T> functionWithIntermediateKey) {
    // Get from cache or lookup previously used key
    CryptoKey intermediateKey = intermediateKeyCache.get(intermediateKeyMeta.getCreated());
    if (intermediateKey == null) {
      intermediateKey = getIntermediateKey(intermediateKeyMeta.getCreated());

      // Put the key into our cache if allowed
      if (cryptoPolicy.canCacheIntermediateKeys()) {
        try {
          logger.debug("attempting to update cache for IK {} with created {}",
              partition.getIntermediateKeyId(), intermediateKey.getCreated());
          intermediateKey = intermediateKeyCache.putAndGetUsable(intermediateKey.getCreated(), intermediateKey);
        }
        catch (Exception e) {
          closeKey(intermediateKey, e);
          throw new AppEncryptionException("Unable to update cache for Intermediate Key", e);
        }
      }
    }

    if (cryptoPolicy.notifyExpiredIntermediateKeyOnRead() && isKeyExpiredOrRevoked(intermediateKey)) {
      // TODO Send notification that a DRK is using an expired IK
      logger.debug("NOTIFICATION: Expired IK {} in use during read", intermediateKeyMeta);
    }

    return applyFunctionAndCloseKey(intermediateKey, functionWithIntermediateKey);
  }

  <T> T withIntermediateKeyForWrite(final Function<CryptoKey, T> functionWithIntermediateKey) {
    // Try to get latest from cache. If not found or expired, get latest or create
    CryptoKey intermediateKey = intermediateKeyCache.getLast();
    if (intermediateKey == null || isKeyExpiredOrRevoked(intermediateKey)) {
      intermediateKey = getLatestOrCreateIntermediateKey();

      // Put the key into our cache if allowed
      if (cryptoPolicy.canCacheIntermediateKeys()) {
        try {
          logger.debug("attempting to update cache for IK {} with created {}",
              partition.getIntermediateKeyId(), intermediateKey.getCreated());
          intermediateKey = intermediateKeyCache.putAndGetUsable(intermediateKey.getCreated(), intermediateKey);
        }
        catch (Exception e) {
          closeKey(intermediateKey, e);
          throw new AppEncryptionException("Unable to update cache for Intermediate Key", e);
        }
      }
    }

    return applyFunctionAndCloseKey(intermediateKey, functionWithIntermediateKey);
  }

  /**
   * Calls a function using a decrypted system key that was previously used.
   *
   * @param systemKeyMeta The {@code SystemKey} meta used previously to write an IK.
   * @param treatExpiredAsMissing If {@code true}, will throw a {@code MetadataMissingException} if the key is
   * expired/revoked.
   * @param functionWithSystemKey The function to call using the decrypted system key.
   * @return The result returned by the {@code functionWithSystemKey}.
   * @throws MetadataMissingException If the system key is not found, or if its expired/revoked and {@code
   * treatExpiredAsMissing} is {@code true}.
   */
  <T> T withExistingSystemKey(final KeyMeta systemKeyMeta, final boolean treatExpiredAsMissing,
      final Function<CryptoKey, T> functionWithSystemKey) {
    // Get from cache or lookup previously used key
    CryptoKey systemKey = systemKeyCache.get(systemKeyMeta.getCreated());
    if (systemKey == null) {
      systemKey = getSystemKey(systemKeyMeta);

      // Put the key into our cache if allowed
      if (cryptoPolicy.canCacheSystemKeys()) {
        try {
          logger.debug("attempting to update cache for SK {}", systemKeyMeta);
          systemKey = systemKeyCache.putAndGetUsable(systemKeyMeta.getCreated(), systemKey);
        }
        catch (Exception e) {
          closeKey(systemKey, e);
          throw new AppEncryptionException("Unable to update cache for SystemKey", e);
        }
      }
    }

    if (isKeyExpiredOrRevoked(systemKey)) {
      if (treatExpiredAsMissing) {
        closeKey(systemKey, null);
        throw new MetadataMissingException("System key is expired/revoked, keyMeta = " + systemKeyMeta);
      }

      // There's an implied else here
      if (cryptoPolicy.notifyExpiredSystemKeyOnRead()) {
        // TODO Send notification that an SK is expired
        logger.debug("NOTIFICATION: Expired SK {} in use during read", systemKeyMeta);
      }
    }

    return applyFunctionAndCloseKey(systemKey, functionWithSystemKey);
  }

  <T> T withSystemKeyForWrite(final Function<CryptoKey, T> functionWithDecryptedSystemKey) {
    // Try to get latest from cache. If not found or expired, get latest or create
    CryptoKey systemKey = systemKeyCache.getLast();
    if (systemKey == null || isKeyExpiredOrRevoked(systemKey)) {
      systemKey = getLatestOrCreateSystemKey();

      // Put the key into our cache if allowed
      if (cryptoPolicy.canCacheSystemKeys()) {
        try {
          KeyMeta systemKeyMeta = new KeyMeta(partition.getSystemKeyId(), systemKey.getCreated());

          logger.debug("attempting to update cache for SK {}", systemKeyMeta);
          systemKey = systemKeyCache.putAndGetUsable(systemKeyMeta.getCreated(), systemKey);
        }
        catch (Exception e) {
          closeKey(systemKey, e);
          throw new AppEncryptionException("Unable to update cache for SystemKey", e);
        }
      }
    }

    return applyFunctionAndCloseKey(systemKey, functionWithDecryptedSystemKey);
  }

  CryptoKey getLatestOrCreateIntermediateKey() {
    Optional<EnvelopeKeyRecord> newestIntermediateKeyRecord = loadLatestKeyRecord(partition.getIntermediateKeyId());
    if (newestIntermediateKeyRecord.isPresent()) {
      // If the key we just got back isn't expired, then just use it
      if (!isKeyExpiredOrRevoked(newestIntermediateKeyRecord.get())) {
        try {
          return withExistingSystemKey(
              newestIntermediateKeyRecord.get().getParentKeyMeta().orElseThrow(
                  () -> new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey")
              ),
              true,
              (key) -> decryptKey(newestIntermediateKeyRecord.get(), key));
        }
        catch (MetadataMissingException e) {
          logger.debug("The SK for the IK ({}, {}) is missing or in an invalid state. Will create new IK instead.",
              partition.getIntermediateKeyId(), newestIntermediateKeyRecord.get().getCreated(), e);
        }
      }

      // If we're here we know we have an expired key.
      // If we're doing queued rotation, flag it and continue to use the expired key
      if (cryptoPolicy.isQueuedKeyRotation()) {
        // TODO Queued rotation
        logger.debug("Queuing up IK {} for rotation", partition.getIntermediateKeyId());
        try {
          // TODO Since not inline rotation, we should pass false to allow using expired SK (what we do for IK,
          // literally here)
          return withExistingSystemKey(
              newestIntermediateKeyRecord.get().getParentKeyMeta().orElseThrow(
                  () -> new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey")
              ),
              true,
              (key) -> decryptKey(newestIntermediateKeyRecord.get(), key));
        }
        catch (MetadataMissingException e) {
          logger.debug("The SK for the IK ({}, {}) is missing or in an invalid state. Will create new IK instead.",
              partition.getIntermediateKeyId(), newestIntermediateKeyRecord.get().getCreated(), e);
        }
      }

      // If we're here then we're doing inline rotation and have an expired key.
      // Fall through as if we didn't have the key
    }

    Instant intermediateKeyCreated = cryptoPolicy.truncateToIntermediateKeyPrecision(Instant.now());
    CryptoKey intermediateKey = crypto.generateKey(intermediateKeyCreated);
    try {
      EnvelopeKeyRecord newIntermediateKeyRecord = withSystemKeyForWrite(
          (systemCryptoKey) -> new EnvelopeKeyRecord(intermediateKey.getCreated(),
          new KeyMeta(partition.getSystemKeyId(), systemCryptoKey.getCreated()),
          crypto.encryptKey(intermediateKey, systemCryptoKey), false));

      logger.debug("attempting to store new IK {} for created {}",
          partition.getIntermediateKeyId(), newIntermediateKeyRecord.getCreated());
      if (metastore.store(partition.getIntermediateKeyId(), newIntermediateKeyRecord.getCreated(),
          newIntermediateKeyRecord.toJson())) {
        return intermediateKey;
      }
      else {
        logger.debug("attempted to store new IK {} but detected duplicate for created {}, closing newly created IK",
            partition.getIntermediateKeyId(), intermediateKey.getCreated());
        closeKey(intermediateKey, null);
      }
    }
    catch (Exception e) {
      closeKey(intermediateKey, e);
      throw new AppEncryptionException("Unable to store new Intermediate Key", e);
    }

    // If we're here, storing of the newly generated key failed above which means we attempted to
    // save a duplicate key to the metastore. If that's the case, then we know a valid key exists
    // in the metastore, so let's grab it and return it.

    // Using a new variable instead of the one above because the withSystemKeyForWrite use above wants finality
    Optional<EnvelopeKeyRecord> actualLatestIntermediateKeyRecord =
        loadLatestKeyRecord(partition.getIntermediateKeyId());

    if (actualLatestIntermediateKeyRecord.isPresent()) {
      // NOTE: Not wrapping this in try/catch to allow errors to bubble up. If we're missing meta in this flow,
      // something's wrong.
      return withExistingSystemKey(
          actualLatestIntermediateKeyRecord.get().getParentKeyMeta().orElseThrow(
              () -> new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey")
          ),
          true,
          (key) -> decryptKey(actualLatestIntermediateKeyRecord.get(), key));
    }
    else {
      throw new AppEncryptionException("IntermediateKey not present after loadLatestKeyRecord retry");
    }
  }

  CryptoKey getLatestOrCreateSystemKey() {
    Optional<EnvelopeKeyRecord> newestSystemKeyRecord = loadLatestKeyRecord(partition.getSystemKeyId());
    if (newestSystemKeyRecord.isPresent()) {
      // If the key we just got back isn't expired, then just use it
      if (!isKeyExpiredOrRevoked(newestSystemKeyRecord.get())) {
        return keyManagementService.decryptKey(newestSystemKeyRecord.get().getEncryptedKey(),
            newestSystemKeyRecord.get().getCreated(), newestSystemKeyRecord.get().isRevoked().orElse(false));
      }

      // If we're here we know we have an expired key.
      // If we're doing queued rotation, flag it and continue to use the expired key
      if (cryptoPolicy.isQueuedKeyRotation()) {
        // TODO Queued rotation
        logger.debug("Queuing up SK {} for rotation", partition.getSystemKeyId());
        return keyManagementService.decryptKey(newestSystemKeyRecord.get().getEncryptedKey(),
            newestSystemKeyRecord.get().getCreated(), newestSystemKeyRecord.get().isRevoked().orElse(false));
      }

      // If we're here then we're doing inline rotation and have an expired key.
      // Fall through as if we didn't have the key
    }

    Instant systemKeyCreated = cryptoPolicy.truncateToSystemKeyPrecision(Instant.now());
    CryptoKey systemKey = crypto.generateKey(systemKeyCreated);
    try {
      EnvelopeKeyRecord newSystemKeyRecord = new EnvelopeKeyRecord(systemKey.getCreated(), null,
          keyManagementService.encryptKey(systemKey), false);

      logger.debug("attempting to store new SK {} for created {}", partition.getSystemKeyId(),
          newSystemKeyRecord.getCreated());
      if (metastore.store(partition.getSystemKeyId(), newSystemKeyRecord.getCreated(),
          newSystemKeyRecord.toJson())) {
        return systemKey;
      }
      else {
        logger.debug("attempted to store new SK {} but detected duplicate for created {}, closing newly created SK",
            partition.getSystemKeyId(), systemKey.getCreated());
        closeKey(systemKey, null);
      }
    }
    catch (Exception e) {
      closeKey(systemKey, e);
      throw new AppEncryptionException("Unable to store new System Key", e);
    }

    // If we're here, storing of the newly generated key failed above which means we attempted to
    // save a duplicate key to the metastore. If that's the case, then we know a valid key exists
    // in the metastore, so let's grab it and return it.
    newestSystemKeyRecord = loadLatestKeyRecord(partition.getSystemKeyId());

    if (newestSystemKeyRecord.isPresent()) {
      return keyManagementService.decryptKey(newestSystemKeyRecord.get().getEncryptedKey(),
          newestSystemKeyRecord.get().getCreated(), newestSystemKeyRecord.get().isRevoked().orElse(false));
    }
    else {
      throw new AppEncryptionException("SystemKey not present after loadLatestKeyRecord retry");
    }
  }

  /**
   * Fetches a known intermediate key from the metastore and decrypts it using its associated system key.
   *
   * @param intermediateKeyCreated Creation time of intermediate key.
   * @return The decrypted intermediate key.
   * @throws MetadataMissingException If the intermediate key is not found, or it has missing system key info.
   */
  CryptoKey getIntermediateKey(final Instant intermediateKeyCreated) {
    EnvelopeKeyRecord intermediateKeyRecord = loadKeyRecord(partition.getIntermediateKeyId(), intermediateKeyCreated);

    return withExistingSystemKey(
        intermediateKeyRecord.getParentKeyMeta().orElseThrow(
            () -> new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey")
        ),
        false,
        (key) -> decryptKey(intermediateKeyRecord, key));
  }

  /**
   * Fetches a known system key from the metastore and decrypts it using the key management service.
   *
   * @param systemKeyMeta The {@link KeyMeta} of the {@code SystemKey}.
   * @return The decrypted system key.
   * @throws MetadataMissingException If the system key is not found.
   */
  CryptoKey getSystemKey(final KeyMeta systemKeyMeta) {
    EnvelopeKeyRecord systemKeyRecord = loadKeyRecord(systemKeyMeta.getKeyId(), systemKeyMeta.getCreated());

    return keyManagementService.decryptKey(systemKeyRecord.getEncryptedKey(), systemKeyRecord.getCreated(),
        systemKeyRecord.isRevoked().orElse(false));
  }

  /**
   * Decrypts the {@code keyRecord}'s encrypted key using the provided key.
   *
   * @param keyRecord The key to decrypt.
   * @param keyEncryptionKey The encryption key to use for decryption.
   * @return The decrypted key contained in the {@link EnvelopeKeyRecord}.
   */
  CryptoKey decryptKey(final EnvelopeKeyRecord keyRecord, final CryptoKey keyEncryptionKey) {
    return crypto.decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), keyEncryptionKey,
        keyRecord.isRevoked().orElse(false));
  }

  /**
   * Gets a specific {@code EnvelopeKeyRecord} or throws a {@code MetadataMissingException} if not found.
   *
   * @param keyId Key id of the record to load.
   * @param created Created time of the record to load.
   * @return The {@link EnvelopeKeyRecord}, if found.
   * @throws MetadataMissingException if the {@code EnvelopeKeyRecord} is not found.
   */
  EnvelopeKeyRecord loadKeyRecord(final String keyId, final Instant created) {
    logger.debug("attempting to load key with keyId {} created {}", keyId, created);
    return metastore.load(keyId, created)
        .map(Json::new)
        .map(EnvelopeKeyRecord::new)
        .orElseThrow(
            () -> new MetadataMissingException("Could not find EnvelopeKeyRecord with keyId=" + keyId + ", created=" +
                      created)
        );
  }

  /**
   * Gets the most recently created key for a given key ID, if any.
   *
   * @param keyId The id to find the latest key of.
   * @return The latest key for the {@code keyId}, if any.
   */
  Optional<EnvelopeKeyRecord> loadLatestKeyRecord(final String keyId) {
    logger.debug("attempting to load latest key with keyId {}", keyId);
    return metastore.loadLatest(keyId)
        .map(Json::new)
        .map(EnvelopeKeyRecord::new);
  }

  boolean isKeyExpiredOrRevoked(final EnvelopeKeyRecord envelopeKeyRecord) {
    return cryptoPolicy.isKeyExpired(envelopeKeyRecord.getCreated()) || envelopeKeyRecord.isRevoked().orElse(false);
  }

  boolean isKeyExpiredOrRevoked(final CryptoKey cryptoKey) {
    return cryptoPolicy.isKeyExpired(cryptoKey.getCreated()) || cryptoKey.isRevoked();
  }

  private <T> T applyFunctionAndCloseKey(final CryptoKey key, final Function<CryptoKey, T> functionWithKey) {
    try {
      return functionWithKey.apply(key);
    }
    catch (Exception e) {
      throw new AppEncryptionException("Failed call action method, error: " + e.getMessage(), e);
    }
    finally {
      closeKey(key, null);
    }
  }

  private void closeKey(final CryptoKey cryptoKey, final Exception rootException) {
    try {
      cryptoKey.close();
    }
    catch (Exception e) {
      // If called w/ root exception, fill in as the cause
      if (rootException != null) {
        e.initCause(rootException);
      }
      throw new AppEncryptionException("Failed to close/wipe key, error: " + e.getMessage(), e);
    }
  }
}


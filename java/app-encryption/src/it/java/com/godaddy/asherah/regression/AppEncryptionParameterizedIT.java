package com.godaddy.asherah.regression;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Partition;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionJsonImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionBytesImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.testhelpers.KeyState;
import com.godaddy.asherah.utils.DateTimeUtils;
import com.godaddy.asherah.utils.PayloadGenerator;
import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.mockito.junit.jupiter.MockitoExtension;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.stream.Stream;

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class AppEncryptionParameterizedIT {
  private static final Logger LOG = LoggerFactory.getLogger(AppEncryptionParameterizedIT.class);
  private static final Random RANDOM = new Random();

  private JSONObject payload;

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createDefaultRandomJsonPayload();
  }

  @SuppressWarnings("unchecked")
  @DisplayName("App Encryption scenarios")
  @ParameterizedTest(name = "{index} => {2}CacheIK{4}MetastoreIK{3}CacheSK{5}MetastoreSK")
  @MethodSource("generateScenarios")
  void parameterizedTests(
    final EnvelopeEncryption<byte[]> envelopeEncryptionJson,
    final Metastore<JSONObject> metastore,
    final KeyState cacheIK, final KeyState metaIK,
    final KeyState cacheSK, final KeyState metaSK,
    final Partition partition) {

    try (Session<JSONObject, byte[]> sessionJsonImpl = new SessionJsonImpl<>(envelopeEncryptionJson)) {

      EncryptMetastoreInteractions encryptMetastoreInteractions =
        new EncryptMetastoreInteractions(cacheIK, metaIK, cacheSK, metaSK);
      DecryptMetastoreInteractions decryptMetastoreInteractions =
        new DecryptMetastoreInteractions(cacheIK, cacheSK);

      //encrypt with library object(sessionJsonImpl)
      byte[] encryptedPayload = sessionJsonImpl.encrypt(payload);

      assertNotNull(encryptedPayload);
      verifyEncryptFlow(metastore, encryptMetastoreInteractions, partition);

      reset(metastore);
      JSONObject decryptedPayload = sessionJsonImpl.decrypt(encryptedPayload);

      verifyDecryptFlow(metastore, decryptMetastoreInteractions, partition);
      assertTrue(payload.similar(decryptedPayload));
    }
  }

  private void verifyEncryptFlow(final Metastore<JSONObject> metastore,
      final EncryptMetastoreInteractions metastoreInteractions, final Partition partition) {

    // If IK is stored to metastore
    if (metastoreInteractions.shouldStoreIK()) {
      verify(metastore)
        .store(eq(partition.getIntermediateKeyId()), any(Instant.class), any(JSONObject.class));
    } else {
      verify(metastore,
        never()).store(eq(partition.getIntermediateKeyId()), any(Instant.class), any(JSONObject.class));
    }
    // If SK is stored to metastore
    if (metastoreInteractions.shouldStoreSK()) {
      verify(metastore)
        .store(eq(partition.getSystemKeyId()), any(Instant.class), any(JSONObject.class));
    } else {
      verify(metastore,
        never()).store(eq(partition.getSystemKeyId()), any(Instant.class), any(JSONObject.class));
    }
    // If neither IK nor SK is stored
    if (!metastoreInteractions.shouldStoreIK() && !metastoreInteractions.shouldStoreSK()) {
      verify(metastore,
        never()).store(any(String.class), any(Instant.class), any(JSONObject.class));
    }

    // NOTE: We do not read IK from the metastore in case of Encrypt
    // If SK is loaded from metastore
    if (metastoreInteractions.shouldLoadSK()) {
      verify(metastore)
        .load(eq(partition.getSystemKeyId()), any(Instant.class));
    } else {
      verify(metastore,
        never()).load(anyString(), any(Instant.class));
    }

    // If latest IK is loaded from metastore
    if (metastoreInteractions.shouldLoadLatestIK()) {
      verify(metastore)
        .loadLatest(eq(partition.getIntermediateKeyId()));
    } else {
      verify(metastore,
        never()).loadLatest(eq(partition.getIntermediateKeyId()));
    }
    // If latest SK is loaded from metastore
    if (metastoreInteractions.shouldLoadLatestSK()) {
      verify(metastore)
        .loadLatest(eq(partition.getSystemKeyId()));
    } else {
      verify(metastore,
        never()).loadLatest(eq(partition.getSystemKeyId()));
    }
    // If neither latest IK or SK is loaded from metastore
    if (!metastoreInteractions.shouldLoadLatestSK() && !metastoreInteractions.shouldLoadLatestIK()) {
      verify(metastore,
        never()).loadLatest(any(String.class));
    }
  }

  private void verifyDecryptFlow(final Metastore<JSONObject> metastore,
      final DecryptMetastoreInteractions metastoreInteractions, final Partition partition) {

    // If IK is loaded from metastore
    if (metastoreInteractions.shouldLoadIK()) {
      verify(metastore)
        .load(eq(partition.getIntermediateKeyId()), any(Instant.class));
    }

    // If SK is loaded from metastore
    if (metastoreInteractions.shouldLoadSK()) {
      verify(metastore)
        .load(eq(partition.getSystemKeyId()), any(Instant.class));
    }
  }

  private static Stream<Arguments> generateScenarios() {
    List<Arguments> listOfScenarios = new ArrayList<>();
    for (KeyState cacheIK : KeyState.values()) {
      for (KeyState metaIK : KeyState.values()) {
        for (KeyState cacheSK : KeyState.values()) {
          for (KeyState metaSK : KeyState.values()) {
            // TODO Add CryptoPolicy.KeyRotationStrategy loop and update expect/verify logic accordingly
            Arguments arguments = generateMocks(cacheIK, metaIK, cacheSK, metaSK);
            listOfScenarios.add(arguments);
          }
        }
      }
    }

    return listOfScenarios.stream();
  }

  private static Arguments generateMocks(final KeyState cacheIK, final KeyState metaIK, final KeyState cacheSK,
      final KeyState metaSK) {

    Partition partition = new Partition(
      cacheIK.toString() + "CacheIK_" + metaIK.toString() + "MetaIK_" +
        DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime() + "_" + RANDOM.nextInt(),
      cacheSK.toString() + "CacheSK_" + metaSK.toString() + "MetaSK_" +
        DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime() + "_" + RANDOM.nextInt(),
      DEFAULT_PRODUCT_ID);

    KeyManagementService kms = TestSetup.createKeyManagemementService();

    CryptoKeyHolder cryptoKeyHolder = CryptoKeyHolder.generateIKSK();

    Metastore<JSONObject> metastore = MetastoreMock.createMetastoreMock(partition, kms,
      TestSetup.createMetastore(), metaIK, metaSK, cryptoKeyHolder);

    CacheMock cacheMock = CacheMock.createCacheMock(cacheIK, cacheSK, cryptoKeyHolder);

    // Mimics (mostly) the old TimeBasedCryptoPolicyImpl settings
    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy.newBuilder()
      .withKeyExpirationDays(KEY_EXPIRY_DAYS)
      .withRevokeCheckMinutes(Integer.MAX_VALUE)
      .withCanCacheIntermediateKeys(false)
      .withCanCacheSystemKeys(false)
      .build();

    SecureCryptoKeyMap<Instant> intermediateKeyCache = cacheMock.getIntermediateKeyCache();
    SecureCryptoKeyMap<Instant> systemKeyCache = cacheMock.getSystemKeyCache();

    EnvelopeEncryptionJsonImpl envelopeEncryptionJson = new EnvelopeEncryptionJsonImpl(
      partition, metastore, systemKeyCache,
      intermediateKeyCache, new BouncyAes256GcmCrypto(),
      cryptoPolicy, kms
    );

    EnvelopeEncryption<byte[]> envelopeEncryptionByteImpl = new EnvelopeEncryptionBytesImpl(envelopeEncryptionJson);

    return Arguments.of(envelopeEncryptionByteImpl, metastore,
      cacheIK, metaIK, cacheSK, metaSK, partition);
  }
}

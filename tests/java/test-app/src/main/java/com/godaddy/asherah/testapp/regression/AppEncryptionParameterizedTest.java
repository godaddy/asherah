package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.AppEncryptionPartition;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionBytesImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.testapp.TestSetup;
import com.godaddy.asherah.testapp.testhelpers.KeyState;
import com.godaddy.asherah.testapp.utils.DateTimeUtils;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;

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

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AppEncryptionParameterizedTest {
  private static final Logger LOG = LoggerFactory.getLogger(AppEncryptionParameterizedTest.class);
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
      final MetastorePersistence<JSONObject> metastorePersistence,
      final KeyState cacheIK, final KeyState metaIK,
      final KeyState cacheSK, final KeyState metaSK,
      final AppEncryptionPartition appEncryptionPartition) {

    try (AppEncryption<JSONObject, byte[]> appEncryptionJsonImpl = new AppEncryptionJsonImpl<>(envelopeEncryptionJson)) {

      EncryptMetastoreInteractions encryptMetastoreInteractions =
          new EncryptMetastoreInteractions(cacheIK, metaIK, cacheSK, metaSK);
      DecryptMetastoreInteractions decryptMetastoreInteractions =
          new DecryptMetastoreInteractions(cacheIK, cacheSK);

      //encrypt with library object(appEncryptionJsonImpl)
      byte[] encryptedPayload = appEncryptionJsonImpl.encrypt(payload);

      assertNotNull(encryptedPayload);
      verifyEncryptFlow(metastorePersistence, encryptMetastoreInteractions, appEncryptionPartition);

      reset(metastorePersistence);
      JSONObject decryptedPayload = appEncryptionJsonImpl.decrypt(encryptedPayload);

      verifyDecryptFlow(metastorePersistence, decryptMetastoreInteractions, appEncryptionPartition);
      assertTrue(payload.similar(decryptedPayload));
    }
  }

  private void verifyEncryptFlow(final MetastorePersistence<JSONObject> metastorePersistence,
      final EncryptMetastoreInteractions metastoreInteractions, final AppEncryptionPartition appEncryptionPartition) {

    // If IK is stored to metastore
    if (metastoreInteractions.shouldStoreIK()) {
      verify(metastorePersistence)
          .store(eq(appEncryptionPartition.getIntermediateKeyId()), any(Instant.class), any(JSONObject.class));
    }
    // If SK is stored to metastore
    if (metastoreInteractions.shouldStoreSK()) {
      verify(metastorePersistence)
          .store(eq(appEncryptionPartition.getSystemKeyId()), any(Instant.class), any(JSONObject.class));
    }
    // If neither IK nor SK is stored
    if (!metastoreInteractions.shouldStoreIK() && !metastoreInteractions.shouldStoreSK()) {
      verify(metastorePersistence,
          never()).store(any(String.class), any(Instant.class), any(JSONObject.class));
    }

    // NOTE: We do not read IK from the metastore in case of Encrypt
    // If SK is loaded from metastore
    if (metastoreInteractions.shouldLoadSK()) {
      verify(metastorePersistence)
          .load(eq(appEncryptionPartition.getSystemKeyId()), any(Instant.class));
    }
    else {
      verify(metastorePersistence,
          never()).load(anyString(), any(Instant.class));
    }

    // If latest IK is loaded from metastore
    if (metastoreInteractions.shouldLoadLatestIK()) {
      verify(metastorePersistence)
          .loadLatestValue(eq(appEncryptionPartition.getIntermediateKeyId()));
    }
    // If latest SK is loaded from metastore
    if (metastoreInteractions.shouldLoadLatestSK()) {
      verify(metastorePersistence)
          .loadLatestValue(eq(appEncryptionPartition.getSystemKeyId()));
    }
    // If neither latest IK or SK is loaded from metastore
    if (!metastoreInteractions.shouldLoadLatestSK() && !metastoreInteractions.shouldLoadLatestIK()) {
      verify(metastorePersistence,
          never()).loadLatestValue(any(String.class));
    }
  }

  private void verifyDecryptFlow(final MetastorePersistence<JSONObject> metastorePersistence,
      final DecryptMetastoreInteractions metastoreInteractions, final AppEncryptionPartition appEncryptionPartition) {

    // If IK is loaded from metastore
    if (metastoreInteractions.shouldLoadIK()) {
      verify(metastorePersistence)
          .load(eq(appEncryptionPartition.getIntermediateKeyId()), any(Instant.class));
    }

    // If SK is loaded from metastore
    if (metastoreInteractions.shouldLoadSK()) {
      verify(metastorePersistence)
          .load(eq(appEncryptionPartition.getSystemKeyId()), any(Instant.class));
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

  private static Arguments generateMocks(final KeyState cacheIK, final KeyState metaIK,
                                         final KeyState cacheSK, final KeyState metaSK) {

    AppEncryptionPartition appEncryptionPartition = new AppEncryptionPartition(
        cacheIK.toString() + "CacheIK_" + metaIK.toString() + "MetaIK_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime() +
            "_" + RANDOM.nextInt(),
        cacheSK.toString() + "CacheSK_" + metaSK.toString() + "MetaSK_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime() +
            "_" + RANDOM.nextInt(),
        DEFAULT_PRODUCT_ID);

    KeyManagementService kms = TestSetup.getKeyManagementService();

    CryptoKeyHolder cryptoKeyHolder = CryptoKeyHolder.generateIKSK();

    MetastorePersistence<JSONObject> metastorePersistence = MetastoreMock.createMetastoreMock(appEncryptionPartition, kms,
        TestSetup.getMetastorePersistence(), metaIK, metaSK, cryptoKeyHolder);

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
        appEncryptionPartition, metastorePersistence, systemKeyCache,
        new FakeSecureCryptoKeyMapFactory<>(intermediateKeyCache), new BouncyAes256GcmCrypto(),
        cryptoPolicy, kms
    );

    EnvelopeEncryption<byte[]> envelopeEncryptionByteImpl = new EnvelopeEncryptionBytesImpl(envelopeEncryptionJson);

    return Arguments.of(envelopeEncryptionByteImpl, metastorePersistence,
        cacheIK, metaIK, cacheSK, metaSK, appEncryptionPartition);
  }
}

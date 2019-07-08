package com.godaddy.asherah.appencryption.json;

import static org.junit.jupiter.api.Assertions.*;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.AppEncryptionPartition;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryptionJsonImpl;
import com.godaddy.asherah.appencryption.envelope.EnvelopeKeyRecord;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.AdhocPersistence;
import com.godaddy.asherah.appencryption.persistence.MemoryPersistenceImpl;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.appencryption.testhelpers.dummy.DummyCryptoPolicy;
import com.godaddy.asherah.appencryption.testhelpers.dummy.DummyKeyManagementService;
import com.godaddy.asherah.appencryption.utils.Json;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMapFactory;

import java.time.Instant;
import java.util.HashMap;
import java.util.Optional;
import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class AppJsonEncryptionImplTest {
  private HashMap<String, JSONObject> memoryPersistence;
  private Persistence<JSONObject> dataPersistence;
  private MetastorePersistence<JSONObject> metastorePersistence;
  private AppEncryptionPartition appEncryptionPartition;
  private KeyManagementService keyManagementService;

  @BeforeEach
  void setUp() {
    appEncryptionPartition = new AppEncryptionPartition("PARTITION", "SYSTEM",
        "PRODUCT");

    memoryPersistence = new HashMap<>();
    dataPersistence = new AdhocPersistence<>(key -> Optional.ofNullable(memoryPersistence.get(key)),
        (key, jsonObject) -> memoryPersistence.put(key, jsonObject));
    
    metastorePersistence = new MemoryPersistenceImpl<>();

    keyManagementService = new DummyKeyManagementService();

    AeadEnvelopeCrypto aeadEnvelopeCrypto = new BouncyAes256GcmCrypto();

    //Generate a dummy systemKey document
    CryptoKey systemKey = aeadEnvelopeCrypto.generateKey();
    byte[] encryptedSystemKey = keyManagementService.encryptKey(systemKey);

    EnvelopeKeyRecord systemKeyRecord = new EnvelopeKeyRecord(Instant.now(), null, encryptedSystemKey);

    //Write out the dummy systemKey record
    memoryPersistence.put(appEncryptionPartition.getSystemKeyId(), systemKeyRecord.toJson());

  }

  @ParameterizedTest
  @ValueSource(strings = {
      "TestString",
      "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ",
      "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘" })
  void roundTrip(String testData) {
    roundTripGeneric(testData, new BouncyAes256GcmCrypto());
  }

  void roundTripGeneric(String testData, AeadEnvelopeCrypto aeadEnvelopeCrypto) {
    CryptoPolicy cryptoPolicy = new DummyCryptoPolicy();
    try (SecureCryptoKeyMap<Instant> systemKeyCache =
             new SecureCryptoKeyMap<>(cryptoPolicy.getRevokeCheckPeriodMillis())) {

      EnvelopeEncryption<JSONObject> envelopeEncryption =
          new EnvelopeEncryptionJsonImpl(
              appEncryptionPartition,
              metastorePersistence,
              systemKeyCache,
              new SecureCryptoKeyMapFactory<Instant>(cryptoPolicy),
              aeadEnvelopeCrypto,
              cryptoPolicy,
              keyManagementService);
      try(AppEncryption<JSONObject, JSONObject> appEncryptionJson = new AppEncryptionJsonImpl<>(envelopeEncryption)) {

        Json testJson = new Json();
        testJson.put("Test", testData);

        String persistenceKey = appEncryptionJson.store(testJson.toJsonObject(), dataPersistence);

        Optional<JSONObject> testJson2 = appEncryptionJson.load(persistenceKey, dataPersistence);
        assertTrue(testJson2.isPresent());
        String resultData = testJson2.get().getString("Test");

        assertEquals(testData, resultData);
      }
    }
  }

}

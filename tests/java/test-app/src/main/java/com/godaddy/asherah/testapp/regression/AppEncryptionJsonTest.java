package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.testapp.utils.DateTimeUtils;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;
import com.godaddy.asherah.testapp.utils.PersistenceFactory;
import com.godaddy.asherah.testapp.utils.SessionFactoryGenerator;

import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

public class AppEncryptionJsonTest {
  private static Persistence<byte[]> persistenceBytes;

  private JSONObject payload;
  private AppEncryptionSessionFactory appEncryptionSessionFactory;
  private String partitionId;
  private AppEncryption<JSONObject, byte[]> appEncryptionJson;
  // TODO Consider adding JSONObject-style envelope/persistence tests as well

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createDefaultRandomJsonPayload();
    appEncryptionSessionFactory = SessionFactoryGenerator.createDefaultAppEncryptionSessionFactory();
    partitionId = DEFAULT_PARTITION_ID + "_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime();
    appEncryptionJson = appEncryptionSessionFactory.getAppEncryptionJson(partitionId);
  }

  @AfterEach
  public void tearDown() {
    appEncryptionJson.close();
    appEncryptionSessionFactory.close();
  }

  @Test
  public void jsonEncryptDecrypt() {
    byte[] dataRowRecord = appEncryptionJson.encrypt(payload);
    JSONObject decryptedPayload = appEncryptionJson.decrypt(dataRowRecord);

    assertTrue(payload.similar(decryptedPayload));
  }

  @Test
  public void jsonEncryptDecryptSameSessionMultipleRounds() {
    // Just loop a bunch of times to verify no surprises
    final int iterations = 40;
    for (int i = 0; i < iterations; i++) {
      byte[] dataRowRecord = appEncryptionJson.encrypt(payload);
      JSONObject decryptedPayload = appEncryptionJson.decrypt(dataRowRecord);

      assertTrue(payload.similar(decryptedPayload));
    }
  }

  @Test
  public void jsonStoreLoad() {
    String persistenceKey = appEncryptionJson.store(payload, persistenceBytes);

    Optional<JSONObject> decryptedJsonPayload = appEncryptionJson.load(persistenceKey, persistenceBytes);

    if (decryptedJsonPayload.isPresent()) {
      assertTrue(payload.similar(decryptedJsonPayload.get()));
    }
    else {
      fail("Json load did not return decrypted payload");
    }
  }

  @Test
  public void jsonLoadInvalidKey() {
    String persistenceKey = "1234";

    Optional<JSONObject> decryptedJsonPayload = appEncryptionJson.load(persistenceKey, persistenceBytes);

    assertFalse(decryptedJsonPayload.isPresent());
  }

  @Test
  public void jsonEncryptDecryptWithDifferentSession() {
    byte[] dataRowRecord = appEncryptionJson.encrypt(payload);

    try (AppEncryption<JSONObject, byte[]> appEncryptionJsonNew = appEncryptionSessionFactory.getAppEncryptionJson(partitionId)) {
      JSONObject decryptedPayload = appEncryptionJsonNew.decrypt(dataRowRecord);

      assertTrue(payload.similar(decryptedPayload));
    }
  }

  @Test
  public void jsonEncryptDecryptWithDifferentPayloads() {
    final JSONObject otherPayload = PayloadGenerator.createDefaultRandomJsonPayload();
    byte[] dataRowRecord1 = appEncryptionJson.encrypt(payload);
    byte[] dataRowRecord2 = appEncryptionJson.encrypt(otherPayload);

    JSONObject decryptedPayload1 = appEncryptionJson.decrypt(dataRowRecord1);
    JSONObject decryptedPayload2 = appEncryptionJson.decrypt(dataRowRecord2);

    assertTrue(payload.similar(decryptedPayload1));
    assertTrue(otherPayload.similar(decryptedPayload2));
  }

  @Test
  public void jsonStoreOverwritePayload() {
    String key = "some_key";
    final JSONObject otherPayload = PayloadGenerator.createDefaultRandomJsonPayload();

    appEncryptionJson.store(key, payload, persistenceBytes);
    appEncryptionJson.store(key, otherPayload, persistenceBytes);
    JSONObject actualResult = appEncryptionJson.load(key, persistenceBytes).get();

    assertTrue(otherPayload.similar(actualResult));
  }

}

package com.godaddy.asherah.regression;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.utils.DateTimeUtils;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.PersistenceFactory;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

public class SessionJsonIT {
  private static Persistence<byte[]> persistenceBytes;

  private JSONObject payload;
  private SessionFactory sessionFactory;
  private String partitionId;
  private Session<JSONObject, byte[]> sessionJson;
  // TODO Consider adding JSONObject-style envelope/persistence tests as well

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createDefaultRandomJsonPayload();
    sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(),
      TestSetup.createMetastore());
    partitionId = DEFAULT_PARTITION_ID + "_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime();
    sessionJson = sessionFactory.getSessionJson(partitionId);
  }

  @AfterEach
  public void tearDown() {
    sessionJson.close();
    sessionFactory.close();
  }

  @Test
  public void jsonEncryptDecrypt() {
    byte[] dataRowRecord = sessionJson.encrypt(payload);
    JSONObject decryptedPayload = sessionJson.decrypt(dataRowRecord);

    assertTrue(payload.similar(decryptedPayload));
  }

  @Test
  public void jsonEncryptDecryptSameSessionMultipleRounds() {
    // Just loop a bunch of times to verify no surprises
    final int iterations = 40;
    for (int i = 0; i < iterations; i++) {
      byte[] dataRowRecord = sessionJson.encrypt(payload);
      JSONObject decryptedPayload = sessionJson.decrypt(dataRowRecord);

      assertTrue(payload.similar(decryptedPayload));
    }
  }

  @Test
  public void jsonStoreLoad() {
    String persistenceKey = sessionJson.store(payload, persistenceBytes);

    Optional<JSONObject> decryptedJsonPayload = sessionJson.load(persistenceKey, persistenceBytes);

    if (decryptedJsonPayload.isPresent()) {
      assertTrue(payload.similar(decryptedJsonPayload.get()));
    } else {
      fail("Json load did not return decrypted payload");
    }
  }

  @Test
  public void jsonLoadInvalidKey() {
    String persistenceKey = "1234";

    Optional<JSONObject> decryptedJsonPayload = sessionJson.load(persistenceKey, persistenceBytes);

    assertFalse(decryptedJsonPayload.isPresent());
  }

  @Test
  public void jsonEncryptDecryptWithDifferentSession() {
    byte[] dataRowRecord = sessionJson.encrypt(payload);

    try (Session<JSONObject, byte[]> sessionJsonNew = sessionFactory.getSessionJson(partitionId)) {
      JSONObject decryptedPayload = sessionJsonNew.decrypt(dataRowRecord);

      assertTrue(payload.similar(decryptedPayload));
    }
  }

  @Test
  public void jsonEncryptDecryptWithDifferentPayloads() {
    final JSONObject otherPayload = PayloadGenerator.createDefaultRandomJsonPayload();
    byte[] dataRowRecord1 = sessionJson.encrypt(payload);
    byte[] dataRowRecord2 = sessionJson.encrypt(otherPayload);

    JSONObject decryptedPayload1 = sessionJson.decrypt(dataRowRecord1);
    JSONObject decryptedPayload2 = sessionJson.decrypt(dataRowRecord2);

    assertTrue(payload.similar(decryptedPayload1));
    assertTrue(otherPayload.similar(decryptedPayload2));
  }

  @Test
  public void jsonStoreOverwritePayload() {
    String key = "some_key";
    final JSONObject otherPayload = PayloadGenerator.createDefaultRandomJsonPayload();

    sessionJson.store(key, payload, persistenceBytes);
    sessionJson.store(key, otherPayload, persistenceBytes);
    JSONObject actualResult = sessionJson.load(key, persistenceBytes).get();

    assertTrue(otherPayload.similar(actualResult));
  }

}

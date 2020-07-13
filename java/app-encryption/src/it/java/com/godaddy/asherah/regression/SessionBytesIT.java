package com.godaddy.asherah.regression;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.utils.DateTimeUtils;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.PersistenceFactory;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

public class SessionBytesIT {
  private static Persistence<byte[]> persistenceBytes;

  private byte[] payload;
  private SessionFactory sessionFactory;
  private String partitionId;
  private Session<byte[], byte[]> sessionBytes;
  // TODO Consider adding JSONObject-style envelope/persistence tests as well

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createDefaultRandomBytePayload();
    sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(),
      TestSetup.createMetastore());
    partitionId = DEFAULT_PARTITION_ID + "_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime();
    sessionBytes = sessionFactory.getSessionBytes(partitionId);
  }

  @AfterEach
  public void tearDown() {
    sessionBytes.close();
    sessionFactory.close();
  }

  @Test
  public void bytesEncryptDecrypt() {
    byte[] dataRowRecord = sessionBytes.encrypt(payload);
    byte[] decryptedPayload = sessionBytes.decrypt(dataRowRecord);

    assertArrayEquals(payload, decryptedPayload);
  }

  @Test
  public void bytesEncryptDecryptSameSessionMultipleRounds() {
    // Just loop a bunch of times to verify no surprises
    final int iterations = 40;
    for (int i = 0; i < iterations; i++) {
      byte[] dataRowRecord = sessionBytes.encrypt(payload);
      byte[] decryptedPayload = sessionBytes.decrypt(dataRowRecord);

      assertArrayEquals(payload, decryptedPayload);
    }
  }

  @Test
  public void bytesStoreLoad() {
    String persistenceKey = sessionBytes.store(payload, persistenceBytes);

    Optional<byte[]> decryptedPayload = sessionBytes.load(persistenceKey, persistenceBytes);

    if (decryptedPayload.isPresent()) {
      assertArrayEquals(payload, decryptedPayload.get());
    }
    else {
      fail("Byte load did not return decrypted payload");
    }
  }

  @Test
  public void bytesLoadInvalidKey() {
    String persistenceKey = "1234";

    Optional<byte[]> decryptedPayload = sessionBytes.load(persistenceKey, persistenceBytes);

    assertFalse(decryptedPayload.isPresent());
  }

  @Test
  public void bytesEncryptDecryptWithDifferentSession() {
    byte[] dataRowRecord = sessionBytes.encrypt(payload);

    try (Session<byte[], byte[]> sessionBytesNew = sessionFactory.getSessionBytes(partitionId)) {
      byte[] decryptedPayload = sessionBytesNew.decrypt(dataRowRecord);

      assertArrayEquals(payload, decryptedPayload);
    }
  }

  @Test
  public void bytesEncryptDecryptWithDifferentPayloads() {
    final byte[] otherPayload = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] dataRowRecord1 = sessionBytes.encrypt(payload);
    byte[] dataRowRecord2 = sessionBytes.encrypt(otherPayload);

    byte[] decryptedPayload1 = sessionBytes.decrypt(dataRowRecord1);
    byte[] decryptedPayload2 = sessionBytes.decrypt(dataRowRecord2);

    assertArrayEquals(payload, decryptedPayload1);
    assertArrayEquals(otherPayload, decryptedPayload2);
  }

  @Test
  public void bytesStoreOverwritePayload() {
    String key = "some_key";
    final byte[] otherPayload = PayloadGenerator.createDefaultRandomBytePayload();

    sessionBytes.store(key, payload, persistenceBytes);
    sessionBytes.store(key, otherPayload, persistenceBytes);
    byte[] decryptedPayload = sessionBytes.load(key, persistenceBytes).get();

    assertArrayEquals(otherPayload, decryptedPayload);
  }

}

package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.testapp.utils.DateTimeUtils;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;
import com.godaddy.asherah.testapp.utils.PersistenceFactory;
import com.godaddy.asherah.testapp.utils.SessionFactoryGenerator;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

public class AppEncryptionBytesTest {
  private static Persistence<byte[]> persistenceBytes;

  private byte[] payload;
  private AppEncryptionSessionFactory appEncryptionSessionFactory;
  private String partitionId;
  private AppEncryption<byte[], byte[]> appEncryptionBytes;
  // TODO Consider adding JSONObject-style envelope/persistence tests as well

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createDefaultRandomBytePayload();
    appEncryptionSessionFactory = SessionFactoryGenerator.createDefaultAppEncryptionSessionFactory();
    partitionId = DEFAULT_PARTITION_ID + "_" + DateTimeUtils.getCurrentTimeAsUtcIsoOffsetDateTime();
    appEncryptionBytes = appEncryptionSessionFactory.getAppEncryptionBytes(partitionId);
  }

  @AfterEach
  public void tearDown() {
    appEncryptionBytes.close();
    appEncryptionSessionFactory.close();
  }

  @Test
  public void bytesEncryptDecrypt() {
    byte[] dataRowRecord = appEncryptionBytes.encrypt(payload);
    byte[] decryptedPayload = appEncryptionBytes.decrypt(dataRowRecord);

    assertArrayEquals(payload, decryptedPayload);
  }

  @Test
  public void bytesEncryptDecryptSameSessionMultipleRounds() {
    // Just loop a bunch of times to verify no surprises
    final int iterations = 40;
    for (int i = 0; i < iterations; i++) {
      byte[] dataRowRecord = appEncryptionBytes.encrypt(payload);
      byte[] decryptedPayload = appEncryptionBytes.decrypt(dataRowRecord);

      assertArrayEquals(payload, decryptedPayload);
    }
  }

  @Test
  public void bytesStoreLoad() {
    String persistenceKey = appEncryptionBytes.store(payload, persistenceBytes);

    Optional<byte[]> decryptedPayload = appEncryptionBytes.load(persistenceKey, persistenceBytes);

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

    Optional<byte[]> decryptedPayload = appEncryptionBytes.load(persistenceKey, persistenceBytes);

    assertFalse(decryptedPayload.isPresent());
  }

  @Test
  public void bytesEncryptDecryptWithDifferentSession() {
    byte[] dataRowRecord = appEncryptionBytes.encrypt(payload);

    try (AppEncryption<byte[], byte[]> appEncryptionBytesNew = appEncryptionSessionFactory.getAppEncryptionBytes(partitionId)) {
      byte[] decryptedPayload = appEncryptionBytesNew.decrypt(dataRowRecord);

      assertArrayEquals(payload, decryptedPayload);
    }
  }

  @Test
  public void bytesEncryptDecryptWithDifferentPayloads() {
    final byte[] otherPayload = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] dataRowRecord1 = appEncryptionBytes.encrypt(payload);
    byte[] dataRowRecord2 = appEncryptionBytes.encrypt(otherPayload);

    byte[] decryptedPayload1 = appEncryptionBytes.decrypt(dataRowRecord1);
    byte[] decryptedPayload2 = appEncryptionBytes.decrypt(dataRowRecord2);

    assertArrayEquals(payload, decryptedPayload1);
    assertArrayEquals(otherPayload, decryptedPayload2);
  }

  @Test
  public void bytesStoreOverwritePayload() {
    String key = "some_key";
    final byte[] otherPayload = PayloadGenerator.createDefaultRandomBytePayload();

    appEncryptionBytes.store(key, payload, persistenceBytes);
    appEncryptionBytes.store(key, otherPayload, persistenceBytes);
    byte[] decryptedPayload = appEncryptionBytes.load(key, persistenceBytes).get();

    assertArrayEquals(otherPayload, decryptedPayload);
  }

}

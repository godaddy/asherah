package com.godaddy.asherah.regression;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.exceptions.MetadataMissingException;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

public class CrossPartitionDecryptIT {

  @Test
  void testCrossPartitionDecryptShouldFail() {
    byte[] payload = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] dataRowRecordBytes;

    String originalPartitionId = "shopper123";
    String alternatePartitionId = "shopper1234";

    SessionFactory sessionFactory = SessionFactoryGenerator
      .createDefaultSessionFactory(TestSetup.createKeyManagemementService(), TestSetup.createMetastore());

    // Encrypt with originalPartitionId
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes(originalPartitionId)) {
      dataRowRecordBytes = sessionBytes.encrypt(payload);
    }

    // Decrypt dataRowString with alternatePartitionId
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes(alternatePartitionId)) {
      assertThrows(MetadataMissingException.class, () -> sessionBytes.decrypt(dataRowRecordBytes));
    }
  }
}

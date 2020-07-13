package com.godaddy.asherah.regression;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.exceptions.MetadataMissingException;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import org.json.JSONObject;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

import static org.junit.jupiter.api.Assertions.assertThrows;

public class CrossPartitionDecryptIT {

  @Test
  void testCrossPartitionDecryptShouldFail() {
    String dataRowString;
    String originalPayloadString;

    InMemoryMetastoreImpl<JSONObject> inMemoryMetastore = new InMemoryMetastoreImpl<>();

    // Encrypt originalPayloadString with partition "shopper123"
    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder("productId", "test")
      .withMetastore(inMemoryMetastore)
      .withCryptoPolicy(new NeverExpiredCryptoPolicy())
      .withKeyManagementService(new StaticKeyManagementServiceImpl("thisIsAStaticMasterKeyForTesting"))
      .build()) {

      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper123")) {

        originalPayloadString = "mysupersecretpayload";

        byte[] dataRowRecordBytes =
          sessionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

        // Consider this us "persisting" the DRR
        dataRowString = Base64.getEncoder().encodeToString(dataRowRecordBytes);
      }
      // Decrypt dataRowString with partition "shopper12345"
      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper12345")) {

        byte[] newDataRowRecordBytes = Base64.getDecoder().decode(dataRowString);

        assertThrows(MetadataMissingException.class, () -> sessionBytes.decrypt(newDataRowRecordBytes));
      }
    }
  }
}

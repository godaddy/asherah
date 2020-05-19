package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.kms.*;
import com.godaddy.asherah.appencryption.persistence.*;
import com.godaddy.asherah.crypto.*;
import org.json.JSONObject;
import org.junit.jupiter.api.Test;

import java.util.HashMap;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;

class AppEncryptionConfigTest {

  final AppEncryptionConfig appEncryptionConfig;

  public AppEncryptionConfigTest() {
    this.appEncryptionConfig = new AppEncryptionConfig();
  }

  @Test
  void testStaticSetupKeyManagementService() {
    KeyManagementService kms = appEncryptionConfig.setupKeyManagementService("static", null, null);

    assertNotNull(kms);
    assertTrue(kms instanceof StaticKeyManagementServiceImpl);
  }

  @Test
  void testAwsSetupKeyManagementService() {
    KeyManagementService kms = appEncryptionConfig.setupKeyManagementService("invalid_value", null, null);
    assertNull(kms);

    kms = appEncryptionConfig.setupKeyManagementService("aws", null, null);
    assertNull(kms);

    Map<String, String> regionArnMap = new HashMap<>();
    regionArnMap.put("region", "arn");
    kms = appEncryptionConfig.setupKeyManagementService("aws", "us-west-2", regionArnMap);
    assertNotNull(kms);
    assertTrue(kms instanceof AwsKeyManagementServiceImpl);
  }

  @Test
  void testInMemorySetupMetastore() {
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("meMoRy", null);

    assertNotNull(metastore);
    assertTrue(metastore instanceof InMemoryMetastoreImpl);
  }

  @Test
  void testDynamoDbSetupMetastore() {
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testJdbcBasedSetupMetastore() {
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("invalid_value", null);
    assertNull(metastore);

    metastore = appEncryptionConfig.setupMetastore("jdbc", null);
    assertNull(metastore);

    metastore = appEncryptionConfig.setupMetastore("jdBC", "someJdbcUrl");
    assertNotNull(metastore);
    assertTrue(metastore instanceof JdbcMetastoreImpl);
  }

  @Test
  void testSetupCryptoPolicy() {
    CryptoPolicy cryptoPolicy = appEncryptionConfig.setupCryptoPolicy(60, 90, 0, 0, false);

    assertNotNull(cryptoPolicy);
    assertTrue(cryptoPolicy instanceof BasicExpiringCryptoPolicy);

    cryptoPolicy = appEncryptionConfig.setupCryptoPolicy(60, 90, 10, 20, true);
    assertNotNull(cryptoPolicy);
    assertTrue(cryptoPolicy instanceof BasicExpiringCryptoPolicy);
  }
}

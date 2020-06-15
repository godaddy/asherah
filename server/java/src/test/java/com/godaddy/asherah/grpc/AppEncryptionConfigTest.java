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
    appEncryptionConfig = new AppEncryptionConfig();
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
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("meMoRy", null, null);

    assertNotNull(metastore);
    assertTrue(metastore instanceof InMemoryMetastoreImpl);
  }

  @Test
  void testDynamoDbSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(null, null, null, null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testDynamoDbWithEmptyEndpointConfigurationSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig("", "", null, null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNull(metastore);
  }

  @Test
  void testDynamoDbWithEndpointConfigurationSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig("endPoint", "us-west-2", null, null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testDynamoDbWithTableNameSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig("endPoint", "us-west-2", null, "CustomTableName");
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testDynamoDbWithEmptyTableNameSetupMetastoreReturnsNull() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig("endPoint", "us-west-2", null, "");
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNull(metastore);
  }

  @Test
  void testDynamoDbWithKeySuffixSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(null, null, "us-west-2", null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testDynamoDbWithEmptyKeySuffixSetupMetastoreReturnsNull() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(null, null, "", null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNull(metastore);
  }

  @Test
  void testDynamoDbWithRegionSetupMetastore() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(null, "us-west-2", "us-west-2", null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNotNull(metastore);
    assertTrue(metastore instanceof DynamoDbMetastoreImpl);
  }

  @Test
  void testDynamoDbWithEmptyRegionSetupMetastoreReturnsNull() {
    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(null, "", null, null);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("dyNaModb", null, dynamoDbConfig);

    assertNull(metastore);
  }

  @Test
  void testJdbcBasedSetupMetastore() {
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore("invalid_value", null, null);
    assertNull(metastore);

    metastore = appEncryptionConfig.setupMetastore("jdbc", null, null);
    assertNull(metastore);

    metastore = appEncryptionConfig.setupMetastore("jdBC", "someJdbcUrl", null);
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

package com.godaddy.asherah.regression;

import com.amazonaws.services.dynamodbv2.local.main.ServerRunner;
import com.amazonaws.services.dynamodbv2.local.server.DynamoDBProxyServer;
import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import software.amazon.awssdk.services.dynamodb.DynamoDbClient;
import software.amazon.awssdk.services.dynamodb.model.AttributeDefinition;
import software.amazon.awssdk.services.dynamodb.model.KeySchemaElement;
import software.amazon.awssdk.services.dynamodb.model.KeyType;
import software.amazon.awssdk.services.dynamodb.model.ProvisionedThroughput;
import software.amazon.awssdk.services.dynamodb.model.ScalarAttributeType;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Arrays;

import static org.junit.jupiter.api.Assertions.*;

class DynamoDbGlobalTableIT {

  private static final String TABLE_NAME = "EncryptionKey";
  private static final String PARTITION_KEY = "Id";
  private static final String SORT_KEY = "Created";
  private static final String DYNAMO_DB_PORT = "8000";
  private static final String REGION_SUFFIX = "us-west-2";
  private static DynamoDBProxyServer server;

  @BeforeEach
  public void setup() throws Exception {
    System.setProperty("sqlite4java.library.path", "target/native-libs");
    String port = DYNAMO_DB_PORT;
    server = ServerRunner.createServerFromCommandLineArgs(
      new String[]{"-inMemory", "-port", port});
    server.start();

    // Setup client pointing to our local dynamodb
    DynamoDbClient dynamoDbClient =
      TestSetup.createDynamoDbClient("http://localhost:" + DYNAMO_DB_PORT, "us-west-2");

    // Create table schema
    dynamoDbClient.createTable(request ->
      request
        .tableName(TABLE_NAME)
        .keySchema(
          KeySchemaElement.builder()
            .attributeName(PARTITION_KEY)
            .keyType(KeyType.HASH)
            .build(),
          KeySchemaElement.builder()
            .attributeName(SORT_KEY)
            .keyType(KeyType.RANGE)
            .build())
        .attributeDefinitions(
          AttributeDefinition.builder()
            .attributeName(PARTITION_KEY)
            .attributeType(ScalarAttributeType.S)
            .build(),
          AttributeDefinition.builder()
            .attributeName(SORT_KEY)
            .attributeType(ScalarAttributeType.N)
            .build())
        .provisionedThroughput(ProvisionedThroughput.builder()
          .readCapacityUnits(1L)
          .writeCapacityUnits(1L)
          .build()));

    dynamoDbClient.close();
  }

  @AfterEach
  public void teardown() throws Exception {
    server.stop();
  }

  private SessionFactory getSessionFactory(boolean withKeySuffix, String region) {
    DynamoDbClient dynamoDbClient =
      TestSetup.createDynamoDbClient("http://localhost:" + DYNAMO_DB_PORT, "us-west-2");
    DynamoDbMetastoreImpl.Builder builder = DynamoDbMetastoreImpl.newBuilder(region);
    builder.withClientOverride(dynamoDbClient);

    if (withKeySuffix) {
      builder.withKeySuffix();
    }

    DynamoDbMetastoreImpl dynamoDbMetastore = builder.build();
    return SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(), dynamoDbMetastore);
  }

  @Test
  void testRegionSuffixBackwardCompatibility() {
    byte[] originalBytes = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] decryptedBytes;
    byte[] dataRowRecordBytes;

    // Encrypt originalPayloadString with metastore without region suffix
    SessionFactory sessionFactory = getSessionFactory(false, REGION_SUFFIX);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      dataRowRecordBytes = sessionBytes.encrypt(originalBytes);
    }

    // Decrypt dataRowString with metastore with region suffix
    sessionFactory = getSessionFactory(true, REGION_SUFFIX);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
    }
    assertTrue(Arrays.equals(decryptedBytes, originalBytes));
  }

  @Test
  void testRegionSuffix() {
    byte[] originalBytes = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] decryptedBytes;
    byte[] dataRowRecordBytes;

    // Encrypt originalPayloadString with metastore with region suffix
    SessionFactory sessionFactory = getSessionFactory(true, REGION_SUFFIX);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      dataRowRecordBytes = sessionBytes.encrypt(originalBytes);
    }

    // Decrypt dataRowString with metastore with region suffix
    sessionFactory = getSessionFactory(true, REGION_SUFFIX);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
    }
    assertTrue(Arrays.equals(decryptedBytes, originalBytes));
  }

  @Test
  void testCrossRegionDecryption() {
    byte[] originalBytes = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] decryptedBytes;
    byte[] dataRowRecordBytes;

    // Encrypt originalPayloadString with metastore with us-west-2 region suffix
    SessionFactory sessionFactory = getSessionFactory(true, REGION_SUFFIX);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      dataRowRecordBytes = sessionBytes.encrypt(originalBytes);
    }

    // Decrypt dataRowString with metastore with us-east-1 region suffix
    sessionFactory = getSessionFactory(true, "us-east-1");
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
    }
    assertTrue(Arrays.equals(decryptedBytes, originalBytes));
  }
}

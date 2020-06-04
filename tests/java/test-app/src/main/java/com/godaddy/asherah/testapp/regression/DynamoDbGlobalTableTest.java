package com.godaddy.asherah.testapp.regression;

import com.amazonaws.client.builder.AwsClientBuilder;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDBClientBuilder;
import com.amazonaws.services.dynamodbv2.document.DynamoDB;
import com.amazonaws.services.dynamodbv2.local.main.ServerRunner;
import com.amazonaws.services.dynamodbv2.local.server.DynamoDBProxyServer;
import com.amazonaws.services.dynamodbv2.model.AttributeDefinition;
import com.amazonaws.services.dynamodbv2.model.CreateTableRequest;
import com.amazonaws.services.dynamodbv2.model.KeySchemaElement;
import com.amazonaws.services.dynamodbv2.model.KeyType;
import com.amazonaws.services.dynamodbv2.model.ProvisionedThroughput;
import com.amazonaws.services.dynamodbv2.model.ScalarAttributeType;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

import static org.junit.jupiter.api.Assertions.assertEquals;

public class DynamoDbGlobalTableTest {

  private static final String TABLE_NAME = "EncryptionKey";
  private static final String PARTITION_KEY = "Id";
  private static final String SORT_KEY = "Created";
  private static final String DYNAMO_DB_PORT = "8000";
  private static DynamoDBProxyServer server;

  @BeforeEach
  public void setup() throws Exception {
    System.setProperty("sqlite4java.library.path", "target/native-libs");
    String port = DYNAMO_DB_PORT;
    server = ServerRunner.createServerFromCommandLineArgs(
      new String[]{"-inMemory", "-port", port});
    server.start();

    // Setup client pointing to our local dynamodb
    DynamoDB dynamoDbDocumentClient = new DynamoDB(
      AmazonDynamoDBClientBuilder.standard()
        .withEndpointConfiguration(new AwsClientBuilder.EndpointConfiguration(
          "http://localhost:" + DYNAMO_DB_PORT, "us-west-2"))
        .build());

    // Create table schema
    dynamoDbDocumentClient.createTable(new CreateTableRequest()
        .withTableName(TABLE_NAME)
        .withKeySchema(
          new KeySchemaElement(PARTITION_KEY, KeyType.HASH),
          new KeySchemaElement(SORT_KEY, KeyType.RANGE))
        .withAttributeDefinitions(
          new AttributeDefinition(PARTITION_KEY, ScalarAttributeType.S),
          new AttributeDefinition(SORT_KEY, ScalarAttributeType.N))
        .withProvisionedThroughput(new ProvisionedThroughput(1L, 1L)));
  }

  @AfterEach
  public void teardown() throws Exception {
    server.stop();
  }

  @Test
  void testTest() {
    String dataRowString;
    String decryptedPayloadString;
    String originalPayloadString;

    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder()
        .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
        .build();

    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder("productId", "reference_app")
      .withMetastore(dynamoDbMetastore)
      .withCryptoPolicy(new NeverExpiredCryptoPolicy())
      .withKeyManagementService(new StaticKeyManagementServiceImpl("thisIsAStaticMasterKeyForTesting"))
      .build()) {

      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper12345")) {

        originalPayloadString = "mysupersecretpayload";

        byte[] dataRowRecordBytes =
          sessionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

        // Consider this us "persisting" the DRR
        dataRowString = Base64.getEncoder().encodeToString(dataRowRecordBytes);
      }
    }

    DynamoDbMetastoreImpl dynamoDbMetastoreWithSuffix = DynamoDbMetastoreImpl.newBuilder()
        .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
        .withDynamoDbRegionSuffix("us-west-2")
        .build();

    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder("productId", "reference_app")
      .withMetastore(dynamoDbMetastoreWithSuffix)
      .withCryptoPolicy(new NeverExpiredCryptoPolicy())
      .withKeyManagementService(new StaticKeyManagementServiceImpl("thisIsAStaticMasterKeyForTesting"))
      .build()) {

      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper12345")) {

        byte[] newDataRowRecordBytes = Base64.getDecoder().decode(dataRowString);

        // Decrypt the payload
        decryptedPayloadString = new String(sessionBytes.decrypt(newDataRowRecordBytes),
          StandardCharsets.UTF_8);
      }
    }
    assertEquals(decryptedPayloadString, originalPayloadString);
  }
}

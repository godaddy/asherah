package com.godaddy.asherah.regression;

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
import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
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
  void testRegionSuffixBackwardCompatibility() {
    byte[] originalBytes = PayloadGenerator.createDefaultRandomBytePayload();
    byte[] decryptedBytes;
    byte[] dataRowRecordBytes;

    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder("us-west-2")
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .build();

    // Encrypt originalPayloadString with metastore without region suffix
    SessionFactory sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(), dynamoDbMetastore);

    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      dataRowRecordBytes = sessionBytes.encrypt(originalBytes);
    }

    DynamoDbMetastoreImpl dynamoDbMetastoreWithSuffix = DynamoDbMetastoreImpl.newBuilder("us-west-2")
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .withKeySuffix()
      .build();

    // Decrypt dataRowString with metastore with region suffix
    sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(), dynamoDbMetastoreWithSuffix);
    try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper12345")) {
      decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
    }
    assertTrue(Arrays.equals(decryptedBytes, originalBytes));
  }
}

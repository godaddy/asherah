package com.godaddy.asherah.appencryption.persistence;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Map;
import java.util.Optional;

import org.json.JSONObject;
//import org.junit.jupiter.api.AfterEach;
//import org.junit.jupiter.api.BeforeEach;
//import org.junit.jupiter.api.Test;
import org.testcontainers.containers.GenericContainer;
//import org.testcontainers.junit.jupiter.Container;
//import org.testcontainers.junit.jupiter.Testcontainers;

import com.amazonaws.SDKGlobalConfiguration;
import com.amazonaws.client.builder.AwsClientBuilder;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDBClientBuilder;
import com.amazonaws.services.dynamodbv2.document.DynamoDB;
import com.amazonaws.services.dynamodbv2.document.Item;
import com.amazonaws.services.dynamodbv2.document.Table;
import com.amazonaws.services.dynamodbv2.model.AttributeDefinition;
import com.amazonaws.services.dynamodbv2.model.CreateTableRequest;
import com.amazonaws.services.dynamodbv2.model.KeySchemaElement;
import com.amazonaws.services.dynamodbv2.model.KeyType;
import com.amazonaws.services.dynamodbv2.model.ProvisionedThroughput;
import com.amazonaws.services.dynamodbv2.model.ScalarAttributeType;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.google.common.collect.ImmutableMap;

import static com.godaddy.asherah.appencryption.persistence.DynamoDbMetastorePersistenceImpl.*;
import static org.junit.jupiter.api.Assertions.*;

//@Testcontainers
class DynamoDbMetastorePersistenceImplTest {

  static final int DYNAMO_DB_PORT = 8000;
  static final String TEST_KEY = "some_key";

//  @Container
  static final GenericContainer<?> DYNAMO_DB_CONTAINER = new GenericContainer<>("amazon/dynamodb-local:latest")
      .withExposedPorts(DYNAMO_DB_PORT);

  // Note we need to use BigDecimals here to make the asserts play nice. SDK is always converting numbers to BigDecimal
  final Map<String, ?> keyRecord = ImmutableMap.of(
      "ParentKeyMeta", ImmutableMap.of(
          "KeyId", "_SK_api_ecomm",
          "Created", new BigDecimal(1541461380)),
      "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
      "Created", new BigDecimal(1541461381));

  final Instant instant = Instant.now().minus(1, ChronoUnit.DAYS);

  DynamoDB dynamoDbDocumentClient;
  Table table;
  DynamoDbMetastorePersistenceImpl dynamoDbMetastorePersistenceImpl;

//  @BeforeEach
  void setUp() {
    // Setup client pointing to our local docker container
    String endpointUrl = String.format("http://%s:%s",
        DYNAMO_DB_CONTAINER.getContainerIpAddress(),
        DYNAMO_DB_CONTAINER.getMappedPort(DYNAMO_DB_PORT));
    dynamoDbDocumentClient = new DynamoDB(
        AmazonDynamoDBClientBuilder.standard()
            .withEndpointConfiguration(new AwsClientBuilder.EndpointConfiguration(endpointUrl, null))
            .build());

    dynamoDbMetastorePersistenceImpl = new DynamoDbMetastorePersistenceImpl(dynamoDbDocumentClient);

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

    table = dynamoDbDocumentClient.getTable(TABLE_NAME);
    Item item = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instant.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, keyRecord);
    table.putItem(item);
  }

//  @AfterEach
  void tearDown() {
    // Blow out the whole table so we have clean slate each time
    dynamoDbDocumentClient.getTable(TABLE_NAME).delete();
  }

//  @Test
  void testLoadSuccess() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.load(TEST_KEY, instant);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

//  @Test
  void testLoadWithNoResultShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.load("fake_key", Instant.now());

    assertFalse(actualJsonObject.isPresent());
  }
//  @Test
  void testLoadWithFailureShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.load(null, Instant.now());

    assertFalse(actualJsonObject.isPresent());
  }

//  @Test
  void testLoadLatestValueWithSingleRecord() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.loadLatestValue(TEST_KEY);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

//  @Test
  void testLoadLatestValueWithMultipleRecords() {
    Instant instantMinusOneHour = instant.minus(1, ChronoUnit.HOURS);
    Instant instantPlusOneHour = instant.plus(1, ChronoUnit.HOURS);
    Instant instantMinusOneDay = instant.minus(1, ChronoUnit.DAYS);
    Instant instantPlusOneDay = instant.plus(1, ChronoUnit.DAYS);
    Item itemMinusOneHour = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instantMinusOneHour.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, ImmutableMap.of(
            "mytime", instantMinusOneHour.getEpochSecond()));
    Item itemPlusOneHour = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instantPlusOneHour.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, ImmutableMap.of(
            "mytime", instantPlusOneHour.getEpochSecond()));
    Item itemMinusOneDay = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instantMinusOneDay.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, ImmutableMap.of(
            "mytime", instantMinusOneDay.getEpochSecond()));
    Item itemPlusOneDay = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instantPlusOneDay.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, ImmutableMap.of(
            "mytime", instantPlusOneDay.getEpochSecond()));
    // intentionally mixing up insertion order
    table.putItem(itemPlusOneHour);
    table.putItem(itemPlusOneDay);
    table.putItem(itemMinusOneHour);
    table.putItem(itemMinusOneDay);

    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.loadLatestValue(TEST_KEY);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(instantPlusOneDay.getEpochSecond(), actualJsonObject.get().getLong("mytime"));
  }

//  @Test
  void testLoadLatestValueWithNoResultShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.loadLatestValue("fake_key");

    assertFalse(actualJsonObject.isPresent());
  }

//  @Test
  void testLoadLatestValueWithFailureShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastorePersistenceImpl.loadLatestValue(null);

    assertFalse(actualJsonObject.isPresent());
  }

//  @Test
  void testStoreSuccess() {
    boolean actualValue = dynamoDbMetastorePersistenceImpl.store(TEST_KEY, Instant.now(), new JSONObject(keyRecord));

    assertTrue(actualValue);
  }

//  @Test
  void testStoreWithDuplicateShouldReturnFalse() {
    Instant now = Instant.now();
    boolean firstAttempt = dynamoDbMetastorePersistenceImpl.store(TEST_KEY, now, new JSONObject(keyRecord));
    boolean secondAttempt = dynamoDbMetastorePersistenceImpl.store(TEST_KEY, now, new JSONObject(keyRecord));

    assertTrue(firstAttempt);
    assertFalse(secondAttempt);
  }

//  @Test
  void testStoreWithFailureShouldThrowException() {
    assertThrows(AppEncryptionException.class,
        () -> dynamoDbMetastorePersistenceImpl.store(null, Instant.now(), new JSONObject()));
  }

//  @Test
  void testPrimaryBuilderPath() {
    // Hack to inject default region since we don't explicitly require one be specified as we do in KMS impl
    System.setProperty(SDKGlobalConfiguration.AWS_REGION_SYSTEM_PROPERTY, "us-west-2");
    DynamoDbMetastorePersistenceImpl.Builder dynamoDbMetastorePersistenceServicePrimaryBuilder =
        DynamoDbMetastorePersistenceImpl.newBuilder();
    DynamoDbMetastorePersistenceImpl dynamoDbMetastorePersistenceImpl =
        dynamoDbMetastorePersistenceServicePrimaryBuilder.build();
    assertNotNull(dynamoDbMetastorePersistenceImpl);
  }

}

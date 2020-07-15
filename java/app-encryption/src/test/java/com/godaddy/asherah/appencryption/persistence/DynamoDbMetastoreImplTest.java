package com.godaddy.asherah.appencryption.persistence;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Map;
import java.util.Optional;

import org.json.JSONObject;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.amazonaws.client.builder.AwsClientBuilder;
import com.amazonaws.SDKGlobalConfiguration;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDB;
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
import com.amazonaws.services.dynamodbv2.local.main.ServerRunner;
import com.amazonaws.services.dynamodbv2.local.server.DynamoDBProxyServer;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.google.common.collect.ImmutableMap;

import static com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl.*;
import static org.junit.jupiter.api.Assertions.*;

class DynamoDbMetastoreImplTest {

  static final String DYNAMO_DB_PORT = "8000";
  static final String TEST_KEY = "some_key";
  static final String REGION = "us-west-2";
  static final String TEST_KEY_WITH_SUFFIX = "some_key_" + REGION;
  static DynamoDBProxyServer server;

  // Note we need to use BigDecimals here to make the asserts play nice. SDK is always converting numbers to BigDecimal
  final Map<String, ?> keyRecord = ImmutableMap.of(
      "ParentKeyMeta", ImmutableMap.of(
          "KeyId", "_SK_api_ecomm",
          "Created", new BigDecimal(1541461380)),
      "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
      "Created", new BigDecimal(1541461381));

  final Instant instant = Instant.now().minus(1, ChronoUnit.DAYS);

  Table table;
  DynamoDB dynamoDbDocumentClient;
  DynamoDbMetastoreImpl dynamoDbMetastoreImpl;

  @BeforeAll
  public static void setupClass() throws Exception {
    System.setProperty("sqlite4java.library.path", "target/native-libs");
    String port = DYNAMO_DB_PORT;
    server = ServerRunner.createServerFromCommandLineArgs(
        new String[]{"-inMemory", "-port", port});
    server.start();
  }

  @AfterAll
  public static void teardownClass() throws Exception {
    server.stop();
  }

  @BeforeEach
  void setUp() {
    dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .build();
    dynamoDbDocumentClient = dynamoDbMetastoreImpl.getClient();

    // Create table schema
    createTableSchema(dynamoDbDocumentClient, dynamoDbMetastoreImpl.getTableName());

    table = dynamoDbDocumentClient.getTable(dynamoDbMetastoreImpl.getTableName());
    Item item = new Item()
        .withPrimaryKey(
            PARTITION_KEY, TEST_KEY,
            SORT_KEY, instant.getEpochSecond())
        .withMap(ATTRIBUTE_KEY_RECORD, keyRecord);
    table.putItem(item);
    Item itemWithSuffix = new Item()
      .withPrimaryKey(
        PARTITION_KEY, TEST_KEY_WITH_SUFFIX,
        SORT_KEY, instant.getEpochSecond())
      .withMap(ATTRIBUTE_KEY_RECORD, keyRecord);
    table.putItem(itemWithSuffix);
  }

  public void createTableSchema(final DynamoDB client,final String tableName) {
    // Create table schema
    client.createTable(new CreateTableRequest()
      .withTableName(tableName)
      .withKeySchema(
        new KeySchemaElement(PARTITION_KEY, KeyType.HASH),
        new KeySchemaElement(SORT_KEY, KeyType.RANGE))
      .withAttributeDefinitions(
        new AttributeDefinition(PARTITION_KEY, ScalarAttributeType.S),
        new AttributeDefinition(SORT_KEY, ScalarAttributeType.N))
      .withProvisionedThroughput(new ProvisionedThroughput(1L, 1L)));
  }

  @AfterEach
  void tearDown() {
    // Blow out the whole table so we have clean slate each time
    dynamoDbDocumentClient.getTable(dynamoDbMetastoreImpl.getTableName()).delete();
  }

  @Test
  void testLoadSuccess() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.load(TEST_KEY, instant);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

  @Test
  void testLoadWithNoResultShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.load("fake_key", Instant.now());

    assertFalse(actualJsonObject.isPresent());
  }
  @Test
  void testLoadWithFailureShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.load(null, Instant.now());

    assertFalse(actualJsonObject.isPresent());
  }

  @Test
  void testLoadLatestWithSingleRecord() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.loadLatest(TEST_KEY);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

  @Test
  void testLoadLatestWithSingleRecordAndSuffix() {
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .withKeySuffix()
      .build();
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.loadLatest(TEST_KEY);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

  @Test
  void testLoadLatestWithMultipleRecords() {
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

    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.loadLatest(TEST_KEY);

    assertTrue(actualJsonObject.isPresent());
    assertEquals(instantPlusOneDay.getEpochSecond(), actualJsonObject.get().getLong("mytime"));
  }

  @Test
  void testLoadLatestWithNoResultShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.loadLatest("fake_key");

    assertFalse(actualJsonObject.isPresent());
  }

  @Test
  void testLoadLatestWithFailureShouldReturnEmpty() {
    Optional<JSONObject> actualJsonObject = dynamoDbMetastoreImpl.loadLatest(null);

    assertFalse(actualJsonObject.isPresent());
  }

  @Test
  void testStoreSuccess() {
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .withKeySuffix()
      .build();
    boolean actualValue = dynamoDbMetastoreImpl.store(TEST_KEY, Instant.now(), new JSONObject(keyRecord));

    assertTrue(actualValue);
  }

  @Test
  void testStoreWithSuffixSuccess() {
    boolean actualValue = dynamoDbMetastoreImpl.store(TEST_KEY, Instant.now(), new JSONObject(keyRecord));

    assertTrue(actualValue);
  }

  @Test
  void testStoreWithDuplicateShouldReturnFalse() {
    Instant now = Instant.now();
    boolean firstAttempt = dynamoDbMetastoreImpl.store(TEST_KEY, now, new JSONObject(keyRecord));
    boolean secondAttempt = dynamoDbMetastoreImpl.store(TEST_KEY, now, new JSONObject(keyRecord));

    assertTrue(firstAttempt);
    assertFalse(secondAttempt);
  }

  @Test
  void testStoreWithFailureShouldThrowException() {
    assertThrows(AppEncryptionException.class,
        () -> dynamoDbMetastoreImpl.store(null, Instant.now(), new JSONObject()));
  }

  @Test
  void testPrimaryBuilderPath() {
    // Hack to inject default region since we don't explicitly require one be specified as we do in KMS impl
    System.setProperty(SDKGlobalConfiguration.AWS_REGION_SYSTEM_PROPERTY, "us-west-2");
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION).build();

    assertNotNull(dynamoDbMetastoreImpl);
  }

  @Test
  void testBuilderPathWithRegion() {
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl =
      DynamoDbMetastoreImpl.newBuilder(REGION).withRegion("us-west-1").build();

    assertNotNull(dynamoDbMetastoreImpl);
  }

  @Test
  void testBuilderPathWithEndPointConfiguration() {
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withEndPointConfiguration("http://localhost:" + DYNAMO_DB_PORT, "us-west-2")
      .build();

    assertNotNull(dynamoDbMetastoreImpl);
  }

  @Test
  void testBuilderPathWithRegionSuffix() {
    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withKeySuffix()
      .build();

    assertFalse(dynamoDbMetastoreImpl.hasKeySuffix());
    assertTrue(dynamoDbMetastore.hasKeySuffix());
  }

  @Test
  void testBuilderPathWithTableName() {
    String tableName = "DummyTable";

    // Use AWS SDK to create client
    AmazonDynamoDB client = AmazonDynamoDBClientBuilder.standard()
      .withEndpointConfiguration(new AwsClientBuilder.EndpointConfiguration("http://localhost:8000", "us-west-2"))
      .build();
    DynamoDB dynamoDBclient = new DynamoDB(client);

    createTableSchema(dynamoDBclient, tableName);

    // Put the object in DummyTable
    Table table = dynamoDBclient.getTable(tableName);
    Item item = new Item()
      .withPrimaryKey(
        PARTITION_KEY, TEST_KEY,
        SORT_KEY, instant.getEpochSecond())
      .withMap(ATTRIBUTE_KEY_RECORD, keyRecord);
    table.putItem(item);

    // Create a metastore object using the withTableName step
    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder(REGION)
      .withEndPointConfiguration("http://localhost:8000", "us-west-2")
      .withTableName(tableName)
      .build();
    Optional<JSONObject> actualJsonObject = dynamoDbMetastore.load(TEST_KEY, instant);

    // Verify that we were able to load and successfully decrypt the item from the
    //metastore object created withTableName
    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }
}

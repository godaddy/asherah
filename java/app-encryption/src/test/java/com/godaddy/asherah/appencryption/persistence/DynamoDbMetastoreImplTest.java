package com.godaddy.asherah.appencryption.persistence;

import java.math.BigDecimal;
import java.net.URI;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Map;
import java.util.Optional;

import com.amazonaws.auth.AWSStaticCredentialsProvider;
import com.amazonaws.auth.BasicAWSCredentials;
import com.amazonaws.client.builder.AwsClientBuilder;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDB;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDBClientBuilder;
import com.amazonaws.services.dynamodbv2.document.DynamoDB;
import com.amazonaws.services.dynamodbv2.document.Item;
import com.amazonaws.services.dynamodbv2.document.Table;
import org.json.JSONObject;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.amazonaws.services.dynamodbv2.local.main.ServerRunner;
import com.amazonaws.services.dynamodbv2.local.server.DynamoDBProxyServer;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.google.common.collect.ImmutableMap;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.AwsCredentialsProvider;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.dynamodb.DynamoDbClient;
import software.amazon.awssdk.services.dynamodb.model.*;

import static com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl.*;
import static org.junit.jupiter.api.Assertions.*;

class DynamoDbMetastoreImplTest {

  static final String DYNAMO_DB_PORT = "8000";
  static final String TEST_KEY = "some_key";
  static final String REGION = "us-west-2";
  static final String TEST_KEY_WITH_SUFFIX = "some_key_" + REGION;
  static DynamoDBProxyServer server;

  final Map<String, ?> keyRecord = ImmutableMap.of(
      "ParentKeyMeta", ImmutableMap.of(
          "KeyId", "_SK_api_ecomm",
          "Created", 1541461380L),
      "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
      "Created", 1541461381L);

  final Instant instant = Instant.now().minus(1, ChronoUnit.DAYS);

  final AwsCredentialsProvider testCredentialsProvider = StaticCredentialsProvider.create(
      AwsBasicCredentials.create("test", "test"));

  DynamoDbMetastoreImpl dynamoDbMetastoreImpl;
  DynamoDbClient dynamoDbClient;
  String tableName;

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
    dynamoDbClient = DynamoDbClient.builder()
      .region(Region.of(REGION))
      .endpointOverride(URI.create("http://localhost:" + DYNAMO_DB_PORT))
      .credentialsProvider(testCredentialsProvider)
      .build();
    dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
      .build();
    tableName = dynamoDbMetastoreImpl.getTableName();

    // Create table schema
    createTableSchema(dynamoDbClient, dynamoDbMetastoreImpl.getTableName());

    putItem(TEST_KEY, instant.getEpochSecond(), keyRecord);
    putItem(TEST_KEY_WITH_SUFFIX, instant.getEpochSecond(), keyRecord);
  }

  public void createTableSchema(final DynamoDbClient dynamoDbClient, final String tableName) {
    // Create table schema
    dynamoDbClient.createTable(request ->
      request
        .tableName(tableName)
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
  }

  @AfterEach
  void tearDown() {
    // Blow out the whole table so we have clean slate each time
    dynamoDbClient.deleteTable(request -> request.tableName(dynamoDbMetastoreImpl.getTableName()));
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
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
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

    // intentionally mixing up insertion order
    putItem(TEST_KEY, instantPlusOneHour.getEpochSecond(), ImmutableMap.of(
      "mytime", instantPlusOneHour.getEpochSecond()));
    putItem(TEST_KEY, instantMinusOneDay.getEpochSecond(), ImmutableMap.of(
      "mytime", instantMinusOneDay.getEpochSecond()));
    putItem(TEST_KEY, instantMinusOneHour.getEpochSecond(), ImmutableMap.of(
      "mytime", instantMinusOneHour.getEpochSecond()));
    putItem(TEST_KEY, instantPlusOneDay.getEpochSecond(), ImmutableMap.of(
      "mytime", instantPlusOneDay.getEpochSecond()));

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
    DynamoDbMetastoreImpl dynamoDbMetastoreImpl = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
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
  void testBuilderPathWithKeySuffix() {
    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
      .withKeySuffix()
      .build();

    assertEquals(REGION, dynamoDbMetastore.getKeySuffix());
  }

  @Test
  void testBuilderPathWithoutKeySuffix() {
    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
      .build();

    assertEquals("", dynamoDbMetastore.getKeySuffix());
  }

  @Test
  void testBuilderPathWithTableName() {
    String tableName = "DummyTable";

    createTableSchema(dynamoDbClient, tableName);

    // Put the object in DummyTable
    putItem(tableName, TEST_KEY, instant.getEpochSecond(), keyRecord);

    // Create a metastore object using the withTableName step
    DynamoDbMetastoreImpl dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder(REGION, dynamoDbClient)
      .withTableName(tableName)
      .build();
    Optional<JSONObject> actualJsonObject = dynamoDbMetastore.load(TEST_KEY, instant);

    // Verify that we were able to load and successfully decrypt the item from the
    //metastore object created withTableName
    assertTrue(actualJsonObject.isPresent());
    assertEquals(keyRecord, actualJsonObject.get().toMap());
  }

  @Test
  void testBackwardCompatibility() {
    // use sdk v1 to store keyrecord
    AmazonDynamoDB client = AmazonDynamoDBClientBuilder.standard()
      .withCredentials(new AWSStaticCredentialsProvider(
        new BasicAWSCredentials("test", "test")))
      .withEndpointConfiguration(
        new AwsClientBuilder.EndpointConfiguration("http://localhost:8000", "us-west-2"))
      .build();

    Table table = new DynamoDB(client).getTable(tableName);

    final Map<String, ?> keyRecordBeforeUpgrade = ImmutableMap.of(
      "ParentKeyMeta", ImmutableMap.of(
        "KeyId", "_SK_api_ecomm",
        "Created", new BigDecimal(1541461380L)),
      "Key", "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
      "Created", new BigDecimal(1541461380L));

    Item item = new Item()
      .withPrimaryKey(
        PARTITION_KEY, "backward-test-key",
        SORT_KEY, instant.getEpochSecond())
      .withMap(ATTRIBUTE_KEY_RECORD, keyRecordBeforeUpgrade);
    table.putItem(item);

    // use sdk v2 to load keyrecord
    JSONObject actualJsonObject = dynamoDbMetastoreImpl.load("backward-test-key", instant).orElse(null);

    // verify
    assertNotNull(actualJsonObject);
    assertEquals(actualJsonObject.get("Key"), keyRecordBeforeUpgrade.get("Key"));
    assertEquals(actualJsonObject.get("Created"), ((BigDecimal) keyRecordBeforeUpgrade.get("Created")).longValueExact());
  }


  private void putItem(String partitionKey, Long sortKey, Map<String, ?> keyRecord) {
    putItem(tableName, partitionKey, sortKey, keyRecord);
  }

  private void putItem(String tableName, String partitionKey, Long sortKey, Map<String, ?> keyRecord) {
    dynamoDbClient.putItem(request ->
      request
        .tableName(tableName)
        .item(Map.of(
          PARTITION_KEY, AttributeValue.fromS(partitionKey),
          SORT_KEY, AttributeValue.fromN(Long.toString(sortKey)),
          ATTRIBUTE_KEY_RECORD, AttributeValue.fromM(DynamoDbMetastoreImpl.toDynamoDbItem(new JSONObject(keyRecord)))
        ))
    );
  }
}

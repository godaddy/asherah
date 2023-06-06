package com.godaddy.asherah.appencryption.persistence;

import java.net.URI;
import java.time.Instant;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.Optional;

import software.amazon.awssdk.core.exception.SdkException;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.dynamodb.DynamoDbClient;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.Timer;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import software.amazon.awssdk.services.dynamodb.DynamoDbClientBuilder;
import software.amazon.awssdk.services.dynamodb.model.AttributeValue;
import software.amazon.awssdk.services.dynamodb.model.ComparisonOperator;
import software.amazon.awssdk.services.dynamodb.model.Condition;
import software.amazon.awssdk.services.dynamodb.model.ConditionalCheckFailedException;
import software.amazon.awssdk.services.dynamodb.model.GetItemResponse;
import software.amazon.awssdk.services.dynamodb.model.QueryResponse;

/**
 * Provides an AWS DynamoDB based implementation of {@link Metastore} to store and retrieve
 * {@link com.godaddy.asherah.appencryption.utils.Json} values for system and intermediate keys. It uses the default
 * table name "EncryptionKey" but that can be configured using {@link Builder#withTableName(String)} " option.
 * Creation time is stored in unix time seconds.
 */
public class DynamoDbMetastoreImpl implements Metastore<JSONObject> {
  static final String DEFAULT_KEY_SUFFIX = "";

  private static final Logger logger = LoggerFactory.getLogger(DynamoDbMetastoreImpl.class);

  static final String PARTITION_KEY = "Id";
  static final String SORT_KEY = "Created";
  static final String ATTRIBUTE_KEY_RECORD = "KeyRecord";

  private final Timer loadTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.load");
  private final Timer loadLatestTimer =
      Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.loadlatest");
  private final Timer storeTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.store");

  private final DynamoDbClient dynamoDbClient;
  private final String tableName;
  private final String preferredRegion;
  private final boolean hasKeySuffix;


  /**
   * Initialize a {@code DynamoDbMetastoreImpl} builder with the given region.
   *
   * @param region The preferred region for the DynamoDb. This can be overridden using the
   * {@link Builder#withRegion(String)} builder step.
   * @return The current {@code Builder} instance.
   */
  public static Builder newBuilder(final String region) {
    return new Builder(region);
  }

  DynamoDbMetastoreImpl(final Builder builder) {
    this.dynamoDbClient = builder.dynamoDbClient;
    this.tableName = builder.tableName;
    this.preferredRegion = builder.preferredRegion;
    this.hasKeySuffix = builder.hasKeySuffix;
  }

  @Override
  public Optional<JSONObject> load(final String keyId, final Instant created) {
    return loadTimer.record(() -> {
      try {
        GetItemResponse getItemResponse = dynamoDbClient.getItem(request ->
            request
              .tableName(tableName)
              .key(Map.of(
                PARTITION_KEY, AttributeValue.fromS(keyId),
                SORT_KEY, AttributeValue.fromN(Long.toString(created.getEpochSecond()))
              ))
              .attributesToGet(ATTRIBUTE_KEY_RECORD)
              .consistentRead(true)  // always use strong consistency
        );
        Map<String, AttributeValue> item = getItemResponse.item();
        if (item != null && item.containsKey(ATTRIBUTE_KEY_RECORD)) {
          return Optional.of(toJSONObject(item.get(ATTRIBUTE_KEY_RECORD).m()));
        }
      }
      catch (SdkException se) {
        logger.error("Metastore error", se);
      }

      return Optional.empty();
    });
  }

  /**
   * Lookup the latest value associated with the keyId.
   *
   * @param keyId The keyId part of the lookup key.
   * @return The latest value associated with the keyId, if any.
   */
  @Override
  public Optional<JSONObject> loadLatest(final String keyId) {
    return loadLatestTimer.record(() -> {
      try {
        // Have to use query api to use limit and reverse sort order
        QueryResponse queryResponse = dynamoDbClient.query(request ->
            request
              .tableName(tableName)
              .keyConditions(Map.of(
                PARTITION_KEY, Condition.builder()
                  .attributeValueList(AttributeValue.fromS(keyId))
                  .comparisonOperator(ComparisonOperator.EQ)
                  .build()
                ))
              .attributesToGet(ATTRIBUTE_KEY_RECORD)
              .scanIndexForward(false) // sorts descending
              .limit(1) // limit 1
              .consistentRead(true) // limit 1
        );
        Iterator<Map<String, AttributeValue>> iterator = queryResponse.items().iterator();
        if (iterator.hasNext()) {
          Map<String, AttributeValue> item = iterator.next();
          return Optional.of(toJSONObject(item.get(ATTRIBUTE_KEY_RECORD).m()));
        }
      }
      catch (SdkException se) {
        logger.error("Metastore error", se);
      }

      return Optional.empty();
    });
  }

  /**
   * Stores the value using the specified keyId and created time.
   *
   * @param keyId The keyId part of the lookup key.
   * @param created The created time part of the lookup key.
   * @param value The value to store.
   * @return {@code true} if the store succeeded, false if the store failed for a known condition
   *         e.g., trying to save a duplicate value should return false, not throw an exception.
   */
  @Override
  public boolean store(final String keyId, final Instant created, final JSONObject value) {
    return storeTimer.record(() -> {
      try {
        // Note conditional expression using attribute_not_exists has special semantics. Can be used on partition OR
        // sort key alone to guarantee primary key uniqueness. It automatically checks for existence of this item's
        // composite primary key and if it contains the specified attribute name, either of which is inherently
        // required.
        dynamoDbClient.putItem(request ->
            request
              .tableName(tableName)
              .item(Map.of(
                PARTITION_KEY, AttributeValue.fromS(keyId),
                SORT_KEY, AttributeValue.fromN(Long.toString(created.getEpochSecond())),
                ATTRIBUTE_KEY_RECORD, AttributeValue.fromM(toDynamoDbItem(value))
              ))
              .conditionExpression("attribute_not_exists(" + PARTITION_KEY + ")")
        );
        return true;
      }
      catch (ConditionalCheckFailedException e) {
        // Duplicate key exists
        logger.info("Attempted to create duplicate key: {} {}", keyId, created);
        return false;
      }
      catch (SdkException se) {
        logger.error("Metastore error during store", se);
        throw new AppEncryptionException("Metastore error", se);
      }
    });
  }

  static Map<String, AttributeValue> toDynamoDbItem(final JSONObject value) {
    Map<String, AttributeValue> result = new HashMap<>();
    for (final var key : value.keySet()) {
      final var object = value.get(key);
      if (object instanceof String stringObject) {
        result.put(key, AttributeValue.fromS(stringObject));
      }
      else if (object instanceof Long longObject) {
        result.put(key, AttributeValue.fromN(Long.toString(longObject)));
      }
      else if (object instanceof Boolean booleanObject) {
        result.put(key, AttributeValue.fromBool(booleanObject));
      }
      else if (object instanceof JSONObject jsonObject) {
        result.put(key, AttributeValue.fromM(toDynamoDbItem(jsonObject)));
      }
      else {
        throw new IllegalArgumentException("Unsupported type: " + object.getClass().getName());
      }
    }
    return result;
  }

  static JSONObject toJSONObject(final Map<String, AttributeValue> item) {
    JSONObject result = new JSONObject();
    for (final var entry : item.entrySet()) {
      final var value = entry.getValue();
      if (value.s() != null) {
        result.put(entry.getKey(), value.s());
      }
      else if (value.n() != null) {
        result.put(entry.getKey(), Long.parseLong(value.n()));
      }
      else if (value.bool() != null) {
        result.put(entry.getKey(), value.bool());
      }
      else if (value.m() != null) {
        result.put(entry.getKey(), toJSONObject(value.m()));
      }
      else {
        throw new IllegalArgumentException("Unsupported type: " + value.type());
      }
    }
    return result;
  }

  @Override
  public String getKeySuffix() {
    if (hasKeySuffix) {
      return preferredRegion;
    }
    return DEFAULT_KEY_SUFFIX;
  }

  String getTableName() {
    return tableName;
  }

  DynamoDbClient getClient() {
    return dynamoDbClient;
  }

  public static final class Builder implements BuildStep, EndPointStep, RegionStep {
    static final String DEFAULT_TABLE_NAME = "EncryptionKey";

    private DynamoDbClient dynamoDbClient;

    private final String preferredRegion;
    private final DynamoDbClientBuilder clientBuilder = DynamoDbClient.builder();

    private String tableName = DEFAULT_TABLE_NAME;
    private boolean hasEndPoint = false;
    private boolean hasKeySuffix = false;
    private boolean hasRegion = false;

    private DynamoDbClient clientOverride;

    private Builder(final String region) {
      this.preferredRegion = region;
    }

    @Override
    public BuildStep withTableName(final String table) {
      this.tableName = table;
      return this;
    }

    @Override
    public BuildStep withEndPointConfiguration(final String endPoint, final String signingRegion) {
      if (!hasRegion) {
        hasEndPoint = true;
        clientBuilder
            .endpointOverride(URI.create(endPoint))
            .region(Region.of(signingRegion));
      }
      return this;
    }

    @Override
    public BuildStep withKeySuffix() {
      this.hasKeySuffix = true;
      return this;
    }

    @Override
    public BuildStep withRegion(final String region) {
      if (!hasEndPoint) {
        hasRegion = true;
        clientBuilder.region(Region.of(region));
      }
      return this;
    }

    @Override
    public BuildStep withClientOverride(final DynamoDbClient client) {
      this.clientOverride = client;
      return this;
    }

    @Override
    public DynamoDbMetastoreImpl build() {
      if (clientOverride != null) {
        dynamoDbClient = clientOverride;
        return new DynamoDbMetastoreImpl(this);
      }
      if (!hasEndPoint && !hasRegion) {
        clientBuilder.region(Region.of(preferredRegion));
      }
      dynamoDbClient = clientBuilder.build();
      return new DynamoDbMetastoreImpl(this);
    }
  }

  public interface EndPointStep {
    /**
     * Adds Endpoint config to the AWS DynamoDb client.
     *
     * @param endPoint The service endpoint either with or without the protocol.
     * @param signingRegion The region to use for SigV4 signing of requests (e.g. us-west-1).
     * @return The current {@code BuildStep} instance with end point configuration.
     */
    BuildStep withEndPointConfiguration(String endPoint, String signingRegion);
  }

  public interface RegionStep {
    /**
     * Specifies the region for the AWS DynamoDb client.
     *
     * @param region The region for the DynamoDb client.
     * @return The current {@code BuildStep} instance with a client region. This overrides the region specified as the
     *         {@code newBuilder} input parameter.
     */
    BuildStep withRegion(String region);
  }

  public interface BuildStep {
    /**
     * Specifies the name of the table.
     *
     * @param tableName A custom name for the table.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withTableName(String tableName);

    /**
     * Specifies whether key suffix should be enabled for DynamoDB. This should be enabled for Global Table support.
     * Adding a suffix to keys prevents multi-region writes from clobbering each other and ensures that no keys are
     * lost.
     *
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withKeySuffix();


    /**
     * Specifies a custom DynamoDb client to be used instead of the default. Custom client will ignore any region
     * or endpoint configuration specified in the builder.
     *
     * @param client A custom DynamoDb client.
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withClientOverride(DynamoDbClient client);

    /**
     * Builds the finalized {@code DynamoDbMetastoreImpl} with the parameters specified in the builder.
     *
     * @return The fully instantiated {@code DynamoDbMetastoreImpl}.
     */
    DynamoDbMetastoreImpl build();
  }
}

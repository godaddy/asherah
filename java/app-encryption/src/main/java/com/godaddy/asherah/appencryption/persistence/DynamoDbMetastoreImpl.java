package com.godaddy.asherah.appencryption.persistence;

import java.time.Instant;
import java.util.Iterator;
import java.util.Optional;

import com.amazonaws.SdkBaseException;
import com.amazonaws.client.builder.AwsClientBuilder;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDB;
import com.amazonaws.services.dynamodbv2.AmazonDynamoDBClientBuilder;
import com.amazonaws.services.dynamodbv2.document.DynamoDB;
import com.amazonaws.services.dynamodbv2.document.GetItemOutcome;
import com.amazonaws.services.dynamodbv2.document.Item;
import com.amazonaws.services.dynamodbv2.document.ItemCollection;
import com.amazonaws.services.dynamodbv2.document.QueryOutcome;
import com.amazonaws.services.dynamodbv2.document.Table;
import com.amazonaws.services.dynamodbv2.document.spec.GetItemSpec;
import com.amazonaws.services.dynamodbv2.document.spec.PutItemSpec;
import com.amazonaws.services.dynamodbv2.document.spec.QuerySpec;
import com.amazonaws.services.dynamodbv2.model.ConditionalCheckFailedException;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.Timer;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Provides an AWS DynamoDB based implementation of {@link Metastore} to store and retrieve
 * {@link com.godaddy.asherah.appencryption.utils.Json} values for system and intermediate keys. It uses the default
 * table name "EncryptionKey" but that can be configured using {@link Builder#withTableName(String)} " option.
 * Creation time is stored in unix time seconds.
 */
public class DynamoDbMetastoreImpl implements Metastore<JSONObject> {
  private static final Logger logger = LoggerFactory.getLogger(DynamoDbMetastoreImpl.class);

  static final String PARTITION_KEY = "Id";
  static final String SORT_KEY = "Created";
  static final String ATTRIBUTE_KEY_RECORD = "KeyRecord";

  private final Timer loadTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.load");
  private final Timer loadLatestTimer =
      Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.loadlatest");
  private final Timer storeTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.dynamodb.store");

  private final DynamoDB client;
  private final String tableName;
  private final String preferredRegion;
  private final boolean hasKeySuffix;
  // Table instance can be cached since thread-safe and no state other than description, which we don't use
  private final Table table;

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
    this.client = new DynamoDB(builder.client);
    this.tableName = builder.tableName;
    this.preferredRegion = builder.preferredRegion;
    this.hasKeySuffix = builder.hasKeySuffix;
    this.table = client.getTable(tableName);
  }

  /**
   * Checks if the metastore has key suffixes enabled, and adds a region suffix to the {@code key} if it does.
   * A key suffix is needed to enable Global Table support. Adding a suffix to keys prevents multi-region writes from
   * clobbering each other.
   *
   * @param key The keyId part of the lookup key.
   * @return The region-suffixed key, if the metastore has that enabled, else returns the same input {@code key}.
   */
  private String getHashKey(final String key) {
    if (this.hasKeySuffix) {
      return key + "_" + this.preferredRegion;
    }

    return key;
  }

  @Override
  public Optional<JSONObject> load(final String keyId, final Instant created) {
    return loadTimer.record(() -> {
      try {
        GetItemOutcome outcome = table.getItemOutcome(new GetItemSpec()
            .withPrimaryKey(
                PARTITION_KEY, keyId,
                SORT_KEY, created.getEpochSecond())
            .withProjectionExpression(ATTRIBUTE_KEY_RECORD)
            .withConsistentRead(true)); // always use strong consistency

        Item item = outcome.getItem();
        if (item != null) {
          return Optional.of(new JSONObject(item.getMap(ATTRIBUTE_KEY_RECORD)));
        }
      }
      catch (SdkBaseException se) {
        logger.error("Metastore error", se);
      }

      return Optional.empty();
    });
  }

  /**
   * Lookup the latest value associated with the keyId. The DynamoDB partition key is formed using the
   * {@link DynamoDbMetastoreImpl#getHashKey(String)} method, which may or may not add a region suffix to it.
   *
   * @param keyId The keyId part of the lookup key.
   * @return The latest value associated with the keyId, if any.
   */
  @Override
  public Optional<JSONObject> loadLatest(final String keyId) {
    return loadLatestTimer.record(() -> {
      try {
        // Have to use query api to use limit and reverse sort order
        ItemCollection<QueryOutcome> itemCollection = table.query(new QuerySpec()
            .withHashKey(PARTITION_KEY, getHashKey(keyId))
            .withProjectionExpression(ATTRIBUTE_KEY_RECORD)
            .withScanIndexForward(false) // sorts descending
            .withMaxResultSize(1) // limit 1
            .withConsistentRead(true)); // always use strong consistency

        Iterator<Item> iterator = itemCollection.iterator();
        if (iterator.hasNext()) {
          Item item = iterator.next();
          return Optional.of(new JSONObject(item.getMap(ATTRIBUTE_KEY_RECORD)));
        }
      }
      catch (SdkBaseException se) {
        logger.error("Metastore error", se);
      }

      return Optional.empty();
    });
  }

  /**
   * Stores the value using the specified keyId and created time. The DynamoDB partition key is formed using the
   * {@link DynamoDbMetastoreImpl#getHashKey(String)} method, which may or may not add a region suffix to it.
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
        Item item = new Item()
            .withPrimaryKey(
                PARTITION_KEY, getHashKey(keyId),
                SORT_KEY, created.getEpochSecond())
            .withMap(ATTRIBUTE_KEY_RECORD, value.toMap());
        table.putItem(new PutItemSpec()
            .withItem(item)
            .withConditionExpression("attribute_not_exists(" + PARTITION_KEY + ")"));

        return true;
      }
      catch (ConditionalCheckFailedException e) {
        // Duplicate key exists
        logger.info("Attempted to create duplicate key: {} {}", keyId, created);
        return false;
      }
      catch (SdkBaseException se) {
        logger.error("Metastore error during store", se);
        throw new AppEncryptionException("Metastore error", se);
      }
    });
  }

  String getTableName() {
    return tableName;
  }

  DynamoDB getClient() {
    return client;
  }

  boolean hasKeySuffix() {
    return hasKeySuffix;
  }

  public static final class Builder implements BuildStep, EndPointStep, RegionStep {
    static final String DEFAULT_TABLE_NAME = "EncryptionKey";

    private AmazonDynamoDB client;
    private final String preferredRegion;
    private final AmazonDynamoDBClientBuilder standardBuilder = AmazonDynamoDBClientBuilder.standard();

    private String tableName = DEFAULT_TABLE_NAME;
    private boolean hasEndPoint = false;
    private boolean hasKeySuffix = false;
    private boolean hasRegion = false;

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
        standardBuilder.withEndpointConfiguration(new AwsClientBuilder.EndpointConfiguration(endPoint, signingRegion));
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
        standardBuilder.withRegion(region);
      }
      return this;
    }

    @Override
    public DynamoDbMetastoreImpl build() {
      if (!hasEndPoint && !hasRegion) {
        standardBuilder.withRegion(preferredRegion);
      }
      client = standardBuilder.build();
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
     * Builds the finalized {@code DynamoDbMetastoreImpl} with the parameters specified in the builder.
     *
     * @return The fully instantiated {@code DynamoDbMetastoreImpl}.
     */
    DynamoDbMetastoreImpl build();
  }
}

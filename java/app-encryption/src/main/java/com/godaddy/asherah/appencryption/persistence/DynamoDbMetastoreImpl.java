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
  private final String regionSuffix;
  // Table instance can be cached since thread-safe and no state other than description, which we don't use
  private final Table table;

  public static Builder newBuilder() {
    return new Builder();
  }

  DynamoDbMetastoreImpl(final Builder builder) {
    this.client = new DynamoDB(builder.client);
    this.tableName = builder.tableName;
    this.regionSuffix = builder.regionSuffix;
    this.table = client.getTable(tableName);
  }

  @Override
  public Optional<JSONObject> load(final String key, final Instant created) {
    return loadTimer.record(() -> {
      try {
        GetItemOutcome outcome = table.getItemOutcome(new GetItemSpec()
            .withPrimaryKey(
                PARTITION_KEY, key,
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

  @Override
  public Optional<JSONObject> loadLatest(final String key) {
    return loadLatestTimer.record(() -> {
      try {
        // Have to use query api to use limit and reverse sort order
        ItemCollection<QueryOutcome> itemCollection = table.query(new QuerySpec()
            .withHashKey(PARTITION_KEY, key)
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

  @Override
  public boolean store(final String key, final Instant created, final JSONObject value) {
    return storeTimer.record(() -> {
      try {
        // Note conditional expression using attribute_not_exists has special semantics. Can be used on partition OR
        // sort key alone to guarantee primary key uniqueness. It automatically checks for existence of this item's
        // composite primary key and if it contains the specified attribute name, either of which is inherently
        // required.
        Item item = new Item()
            .withPrimaryKey(
                PARTITION_KEY, key,
                SORT_KEY, created.getEpochSecond())
            .withMap(ATTRIBUTE_KEY_RECORD, value.toMap());
        table.putItem(new PutItemSpec()
            .withItem(item)
            .withConditionExpression("attribute_not_exists(" + PARTITION_KEY + ")"));

        return true;
      }
      catch (ConditionalCheckFailedException e) {
        // Duplicate key exists
        logger.info("Attempted to create duplicate key: {} {}", key, created);
        return false;
      }
      catch (SdkBaseException se) {
        logger.error("Metastore error during store", se);
        throw new AppEncryptionException("Metastore error", se);
      }
    });
  }

  @Override
  public String getRegionSuffix() {
    return regionSuffix;
  }

  String getTableName() {
    return tableName;
  }

  DynamoDB getClient() {
    return client;
  }

  public static final class Builder implements BuildStep, EndPointStep, RegionStep {

    private AmazonDynamoDB client;

    private String regionSuffix = "";
    private AmazonDynamoDBClientBuilder standardBuilder = AmazonDynamoDBClientBuilder.standard();
    private String tableName = "EncryptionKey";


    @Override
    public BuildStep withEndPointConfiguration(final String endPoint, final String signingRegion) {
      standardBuilder.withEndpointConfiguration(new AwsClientBuilder.EndpointConfiguration(endPoint, signingRegion));
      return this;
    }

    @Override
    public BuildStep withTableName(final String table) {
      this.tableName = table;
      return this;
    }

    @Override
    public BuildStep withDynamoDbRegionSuffix(final String suffix) {
      this.regionSuffix = suffix;
      return this;
    }

    @Override
    public BuildStep withRegion(final String region) {
      standardBuilder.withRegion(region);
      return this;
    }

    @Override
    public DynamoDbMetastoreImpl build() {
      client = standardBuilder.build();
      return new DynamoDbMetastoreImpl(this);
    }
  }

  public interface EndPointStep {
    /**
     * Adds EndPoint config to the AWS DynamoDb client
     * @param endPoint the service endpoint either with or without the protocol
     * @param signingRegion the region to use for SigV4 signing of requests (e.g. us-west-1)
     * @return The current {@code BuildStep} instance
     */
    BuildStep withEndPointConfiguration(String endPoint, String signingRegion);
  }

  public interface RegionStep {
    /**
     * Specifies the region for the AWS DynamoDb client
     * @param region
     * @return The current {@code BuildStep} instance
     */
    BuildStep withRegion(String region);
  }

  public interface BuildStep {
    /**
     * Specifies whether region suffix should be enabled for DynamoDB
     * @param suffix The region to be used as suffix
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withDynamoDbRegionSuffix(String suffix);

    /**
     * Specifies the name of the table
     * @param table The name of the table
     * @return The current {@code BuildStep} instance.
     */
    BuildStep withTableName(String table);

    /**
     * Builds the finalized {@code DynamoDbMetastoreImpl} with the parameters specified in the builder.
     * @return The fully instantiated {@code DynamoDbMetastoreImpl}.
     */
    DynamoDbMetastoreImpl build();
  }
}

package com.godaddy.asherah.referenceapp;

import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.TimeUnit;
import java.util.stream.IntStream;

import com.amazonaws.services.cloudwatch.AmazonCloudWatchAsync;
import com.amazonaws.services.cloudwatch.AmazonCloudWatchAsyncClientBuilder;
import com.codahale.metrics.ConsoleReporter;
import com.codahale.metrics.MetricRegistry;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.appencryption.keymanagement.AWSKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.keymanagement.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastorePersistenceImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastorePersistenceImpl;
import com.godaddy.asherah.appencryption.persistence.MemoryPersistenceImpl;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.google.common.collect.ImmutableMap;
import com.zaxxer.hikari.HikariDataSource;

import io.micrometer.cloudwatch.CloudWatchConfig;
import io.micrometer.cloudwatch.CloudWatchMeterRegistry;
import io.micrometer.core.instrument.Clock;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.dropwizard.DropwizardConfig;
import io.micrometer.core.instrument.dropwizard.DropwizardMeterRegistry;
import io.micrometer.core.instrument.util.HierarchicalNameMapper;
import picocli.CommandLine;
import picocli.CommandLine.Command;
import picocli.CommandLine.Option;

/**
 * Hello world!
 */
@Command(mixinStandardHelpOptions = true, description = "Runs an example end-to-end encryption and decryption round "
                                                      + "trip. NOTE: Some metastore and kms types depend on required "
                                                      + "parameters. Refer to parameter descriptions for which types "
                                                      + "they may be required for.")
public final class App implements Callable<Void> {
  private static final Logger logger = LoggerFactory.getLogger(App.class);

  private static final int KEY_EXPIRATION_DAYS = 30;
  private static final int CACHE_CHECK_MINUTES = 30;

  enum Metastore { MEMORY, JDBC, DYNAMODB }

  enum Kms { STATIC, AWS }

  @Option(names = "--metastore-type", defaultValue = "MEMORY",
      description = "Type of metastore persistence to use. Enum values: ${COMPLETION-CANDIDATES}")
  private Metastore metastore;
  @Option(names = "--jdbc-url",
      description = "JDBC URL to use for JDBC metastore persistence. Required for JDBC metastore.")
  private String jdbcUrl;

  @Option(names = "--kms-type", defaultValue = "STATIC",
      description = "Type of key management service to use. Enum values: ${COMPLETION-CANDIDATES}")
  private Kms kms;
  @Option(names = "--preferred-region",
      description = "Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.")
  private String preferredRegion;
  @Option(names = "--region-arn-tuples", split = ",",
      description = "Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.")
  private Map<String, String> regionMap;

  @Option(names = "--iterations", description = "Number of encrypt/decrypt iterations to run", defaultValue = "1")
  private int iterations;
  @Option(names = "--enable-cw", description = "Enable CloudWatch Metrics output")
  private boolean enableCloudWatch;

  @Option(names = "--drr", description = "DRR to be decrypted")
  private String drr;

  private App() {
  }

  public static void main(final String[] args) {
    CommandLine.call(new App(), args);
  }

  @Override
  public Void call() throws Exception {
    MetastorePersistence<JSONObject> metastorePersistence;
    if (metastore == Metastore.JDBC) {
      if (jdbcUrl != null) {
        logger.info("using JDBC-based metastore...");

        // Setup JDBC persistence from command line argument using Hikari connection pooling
        HikariDataSource dataSource = new HikariDataSource();
        dataSource.setJdbcUrl(jdbcUrl);
        metastorePersistence = JdbcMetastorePersistenceImpl.newBuilder(dataSource).build();
      }
      else {
        CommandLine.usage(this, System.out);
        return null;
      }
    }
    else if (metastore == Metastore.DYNAMODB) {
      logger.info("using DynamoDB-based metastore...");

      metastorePersistence = DynamoDbMetastorePersistenceImpl.newBuilder().build();
    }
    else {
      logger.info("using in-memory metastore...");

      metastorePersistence = new MemoryPersistenceImpl<>();
    }

    KeyManagementService keyManagementService;
    if (kms == Kms.AWS) {
      if (preferredRegion != null && regionMap != null) {
        logger.info("using AWS KMS...");

        // build the ARN regions including preferred region
        keyManagementService = AWSKeyManagementServiceImpl.newBuilder(regionMap, preferredRegion).build();
      }
      else {
        CommandLine.usage(this, System.out);
        return null;
      }
    }
    else {
      logger.info("using static KMS...");

      keyManagementService = new StaticKeyManagementServiceImpl("secretmasterkey!");
    }

    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
        .newBuilder()
        .withKeyExpirationDays(KEY_EXPIRATION_DAYS)
        .withRevokeCheckMinutes(CACHE_CHECK_MINUTES)
        .build();

    // Setup console metrics output (need to use dropwizard hack as micrometer doesn't have console out of the box)
    MetricRegistry consoleRegistry = new MetricRegistry();
    ConsoleReporter consoleReporter = ConsoleReporter.forRegistry(consoleRegistry)
        .convertRatesTo(TimeUnit.SECONDS)
        .convertDurationsTo(TimeUnit.MILLISECONDS)
        .build();
    Metrics.addRegistry(createConsoleMeterRegistry(consoleRegistry));

    // CloudWatch metrics generation
    AmazonCloudWatchAsync amazonCloudWatchAsync = null;
    if (enableCloudWatch) {
      logger.info("enabling CloudWatch metrics...");
      amazonCloudWatchAsync = AmazonCloudWatchAsyncClientBuilder.standard().build();
      setupCloudWatchMeterRegistry(amazonCloudWatchAsync);
    }

    // Create a session factory for this app. Normally this would be done upon app startup and the
    // same factory would be used anytime a new session is needed for a partition (e.g., shopper).
    // We've split it out into multiple try blocks to underscore this point.
    try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
        .newBuilder("productId", "reference_app")
        .withMetastorePersistence(metastorePersistence)
        .withCryptoPolicy(cryptoPolicy)
        .withKeyManagementService(keyManagementService)
        .withMetricsEnabled()
        .build()) {

      // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
      // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
      try (AppEncryption<byte[], byte[]> appEncryptionBytes = appEncryptionSessionFactory
          .getAppEncryptionBytes("shopper123")) {

        String originalPayloadString = "mysupersecretpayload";

        IntStream.range(0, iterations).forEach(i -> {
          String dataRowString;
          // If we get a DRR as a command line argument, we want to directly decrypt it
          if (drr != null) {
            dataRowString = drr;
          }
          else {
            // Encrypt the payload
            byte[] dataRowRecordBytes =
                appEncryptionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

            // Consider this us "persisting" the DRR
            dataRowString = Base64.getEncoder().encodeToString(dataRowRecordBytes);
          }
          logger.info("dataRowRecord as string = {}", dataRowString);

          byte[] newDataRowRecordBytes = Base64.getDecoder().decode(dataRowString);

          // Decrypt the payload
          String decryptedPayloadString = new String(appEncryptionBytes.decrypt(newDataRowRecordBytes),
              StandardCharsets.UTF_8);

          logger.info("decryptedPayloadString = {}, matches = {}", decryptedPayloadString,
              originalPayloadString.equals(decryptedPayloadString));
        });
      }
    }

    // Force final publish of metrics
    consoleReporter.report();
    Metrics.globalRegistry.close();
    if (amazonCloudWatchAsync != null) {
      // Need to let async publish complete and explicitly shut down async client
      final long sleepMillis = 5000;
      Thread.sleep(sleepMillis);
      amazonCloudWatchAsync.shutdown();
    }
    return null;
  }

  private MeterRegistry createConsoleMeterRegistry(final MetricRegistry consoleRegistry) {
    DropwizardConfig consoleConfig = new DropwizardConfig() {

      @Override
      public String prefix() {
        return "console";
      }

      @Override
      public String get(final String key) {
        return null;
      }
    };

    return new DropwizardMeterRegistry(consoleConfig, consoleRegistry, HierarchicalNameMapper.DEFAULT, Clock.SYSTEM) {
      @Override
      protected Double nullGaugeValue() {
        return null;
      }
    };
  }

  private void setupCloudWatchMeterRegistry(final AmazonCloudWatchAsync amazonCloudWatchAsync) {
    CloudWatchConfig cloudWatchConfig = new CloudWatchConfig() {
      private final Map<String, String> props = ImmutableMap.of(
          "cloudwatch.namespace", "AppEncryptionReferenceApp",
          "cloudwatch.step", "PT10S"
      );

      @Override
      public String get(final String key) {
        return props.get(key);
      }
    };
    MeterRegistry cloudWatchRegistry = new CloudWatchMeterRegistry(cloudWatchConfig, Clock.SYSTEM,
        amazonCloudWatchAsync);
    Metrics.addRegistry(cloudWatchRegistry);
  }

}

package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.CryptoPolicy;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Map;
import java.util.concurrent.Callable;

import picocli.CommandLine;

class ServerApp implements Callable<Void> {
  static class MetastoreTypes extends ArrayList<String> {
    MetastoreTypes() {
      super(Arrays.asList(Constants.METASTORE_INMEMORY, Constants.METASTORE_JDBC, Constants.METASTORE_DYNAMODB));
    }
  }

  static class KmsTypes extends ArrayList<String> {
    KmsTypes() {
      super(Arrays.asList(Constants.KMS_STATIC, Constants.KMS_AWS));
    }
  }

  @CommandLine.ArgGroup
  private DynamoDbFlags dynamoDbFlags;

  static class DynamoDbFlags {
    @CommandLine.ArgGroup(exclusive = false)
    private EndPoint endPoint;

    static class EndPoint {
      @CommandLine.Option(names = "--dynamodb-endpoint", required = true,
          defaultValue = "${env:ASHERAH_DYNAMODB_ENDPOINT}",
          description = "The DynamoDb service endpoint (only supported by DYNAMODB)")
      private static String endpoint;
      @CommandLine.Option(names = "--dynamodb-signing-region", required = true,
          defaultValue = "${env:ASHERAH_DYNAMODB_SIGNING_REGION}",
          description = "The DynamoDb region to use for SigV4 signing of requests (only supported by DYNAMODB)")
      private static String signingRegion;
    }

    @CommandLine.ArgGroup
    private Region region;

    static class Region {
      @CommandLine.Option(names = "--dynamodb-region", defaultValue = "${env:ASHERAH_DYNAMODB_REGION}",
          description = "The AWS region for DynamoDB requests (only supported by DYNAMODB)")
      private static String region;
    }
  }

  // Options to configure the metastore
  @CommandLine.Option(names = "--metastore-type", defaultValue = "${env:ASHERAH_METASTORE_MODE}",
      completionCandidates = MetastoreTypes.class,
      description = "Type of metastore to use. Possible values: ${COMPLETION-CANDIDATES}", required = true)
  private static String metastoreType;
  @CommandLine.Option(names = "--jdbc-url", defaultValue = "${env:ASHERAH_CONNECTION_STRING}",
      description = "JDBC URL to use for JDBC metastore. Required for JDBC metastore.")
  private static String jdbcUrl;
  @CommandLine.Option(names = "--enable-key-suffix", defaultValue = "${env:ASHERAH_ENABLE_KEY_SUFFIX}",
      description = "Configure the metastore to use regional suffixes (only supported by DYNAMODB)")
  private String keySuffix;
  @CommandLine.Option(names = "--dynamodb-table-name", defaultValue = "${env:ASHERAH_DYNAMODB_TABLE_NAME}",
      description = "The table name for DynamoDb (only supported by DYNAMODB)")
  private String dynamoDbTableName;

  // Options to configure the KMS
  @CommandLine.Option(names = "--kms-type", defaultValue = "${env:ASHERAH_KMS_MODE}",
      completionCandidates = KmsTypes.class,
      description = "Type of key management service to use. Possible values: ${COMPLETION-CANDIDATES}", required = true)
  private static String kmsType;
  @CommandLine.Option(names = "--preferred-region", defaultValue = "${env:ASHERAH_PREFERRED_REGION}",
      description = "Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.")
  private static String preferredRegion;
  @CommandLine.Option(names = "--region-arn-tuples", defaultValue = "${env:ASHERAH_REGION_MAP}", split = ",",
      description = "Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.")
  private static Map<String, String> regionMap;

  // Options to configure the server
  @CommandLine.Option(names = "--product-id", defaultValue = "${env:ASHERAH_PRODUCT_NAME}",
      description = "Specify the product id", required = true)
  private static String productId;
  @CommandLine.Option(names = "--service-id", defaultValue = "${env:ASHERAH_SERVICE_NAME}",
      description = "Specify the service id", required = true)
  private static String serviceId;
  @CommandLine.Option(names = "--uds", description = "Unix domain socket file",
      defaultValue = Constants.DEFAULT_UDS_PATH)
  private static String udsFilePath;

  // Options to set up crypto policy
  @CommandLine.Option(names = "--key-expiration-days", defaultValue = "${env:ASHERAH_EXPIRE_AFTER}",
      description = "The number of days after which a key will expire", required = true)
  private static int keyExpirationDays;
  @CommandLine.Option(names = "--revoke-check-minutes", defaultValue = "${env:ASHERAH_CHECK_INTERVAL}",
      description = "Sets the cache's TTL in minutes to revoke the keys in the cache", required = true)
  private static int revokeCheckMinutes;
  @CommandLine.Option(names = "--session-caching", defaultValue = "false",
      description = "Enable/disable the session caching")
  private static boolean sessionCacheEnabled;
  @CommandLine.Option(names = "--session-cache-max-size", defaultValue = "1000",
      description = "Define the number of maximum sessions to cache.")
  private static int sessionCacheMaxSize;
  @CommandLine.Option(names = "--session-cache-expire-minutes", defaultValue = "120",
      description = "Evict the session from cache after some minutes.")
  private static int sessionCacheExpireMinutes;

  @Override
  public Void call() throws Exception {

    AppEncryptionConfig appEncryptionConfig = new AppEncryptionConfig();

    KeyManagementService keyManagementService =
        appEncryptionConfig.setupKeyManagementService(kmsType, preferredRegion, regionMap);

    DynamoDbConfig dynamoDbConfig = new DynamoDbConfig(DynamoDbFlags.EndPoint.endpoint,
        DynamoDbFlags.EndPoint.signingRegion,
        DynamoDbFlags.Region.region,
        keySuffix,
        dynamoDbTableName);
    Metastore<JSONObject> metastore = appEncryptionConfig.setupMetastore(metastoreType,
        jdbcUrl,
        dynamoDbConfig);

    if (keyManagementService == null || metastore == null) {
      CommandLine.usage(this, System.out);
      System.exit(-1);
    }

    CryptoPolicy cryptoPolicy = appEncryptionConfig.setupCryptoPolicy(keyExpirationDays, revokeCheckMinutes,
        sessionCacheMaxSize, sessionCacheExpireMinutes, sessionCacheEnabled);

    try (SessionFactory sessionFactory = SessionFactory
        .newBuilder(productId, serviceId)
        .withMetastore(metastore)
        .withCryptoPolicy(cryptoPolicy)
        .withKeyManagementService(keyManagementService)
        .build()) {

      AppEncryptionServer appEncryptionServer = new AppEncryptionServer(sessionFactory, udsFilePath);

      appEncryptionServer.start();
      appEncryptionServer.blockUntilShutdown();
    }

    return null;
  }

  public static void main(final String[] args) {
    // Populate all the arguments
    new CommandLine(new ServerApp()).execute(args);
  }
}

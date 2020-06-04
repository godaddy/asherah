package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.kms.AwsKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.zaxxer.hikari.HikariDataSource;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Map;

class AppEncryptionConfig {
  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionConfig.class);

  KeyManagementService setupKeyManagementService(final String kmsType, final String preferredRegion,
      final Map<String, String> regionMap) {
    switch (kmsType.toUpperCase()) {
      case Constants.KMS_AWS:
        if (preferredRegion == null || regionMap == null) {
          logger.error("aws kms requires both region map and preferred region...");
          break;
        }

        logger.info("using AWS KMS...");
        // build the ARN regions including preferred region
        return AwsKeyManagementServiceImpl
            .newBuilder(regionMap, preferredRegion)
            .build();

      case Constants.KMS_STATIC:
        logger.info("using static KMS...");
        return new StaticKeyManagementServiceImpl("thisIsAStaticMasterKeyForTesting");

      default:
        logger.error("unable to evaluate kms config");
    }

    return null;
  }

  Metastore<JSONObject> setupMetastore(final String metastoreType, final String jdbcUrl) {
    switch (metastoreType.toUpperCase()) {
      case Constants.METASTORE_JDBC:
        if (jdbcUrl == null) {
          logger.error("jdbc url is required for jdbc metastore...");
          break;
        }

        logger.info("using JDBC-based metastore...");
        // Setup JDBC persistence from command line argument using Hikari connection pooling
        HikariDataSource dataSource = new HikariDataSource();
        dataSource.setJdbcUrl(jdbcUrl);
        return JdbcMetastoreImpl
            .newBuilder(dataSource)
            .build();

      case Constants.METASTORE_DYNAMODB:
        logger.info("using DynamoDB-based metastore...");
        return DynamoDbMetastoreImpl.newBuilder().build();

      case Constants.METASTORE_INMEMORY:
        logger.info("using in-memory metastore...");
        return new InMemoryMetastoreImpl<>();

      default:
        logger.error("unable to evaluate metastore config");
    }

    return null;
  }

  CryptoPolicy setupCryptoPolicy(final int keyExpirationDays, final int revokeCheckMinutes,
      final int sessionCacheMaxSize, final int sessionCacheExpireMinutes, final boolean sessionCacheEnabled) {

    logger.info("key expiration days set to = {} days", keyExpirationDays);
    logger.info("revoke check minutes set to = {} minutes", revokeCheckMinutes);

    if (sessionCacheEnabled) {
      logger.info("session caching enabled");
      logger.info("session cache max size = {}", sessionCacheMaxSize);
      logger.info("session cache expire minutes = {} minutes", sessionCacheExpireMinutes);
    }

    return BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(keyExpirationDays)
      .withRevokeCheckMinutes(revokeCheckMinutes)
      .withCanCacheSessions(sessionCacheEnabled)
      .withSessionCacheMaxSize(sessionCacheMaxSize)
      .withSessionCacheExpireMinutes(sessionCacheExpireMinutes)
      .build();
  }
}

package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.AwsKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.google.common.util.concurrent.MoreExecutors;
import com.zaxxer.hikari.HikariDataSource;
import org.apache.commons.lang3.SystemUtils;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.Executor;

import io.grpc.Server;
import io.grpc.netty.NettyServerBuilder;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.epoll.EpollEventLoopGroup;
import io.netty.channel.epoll.EpollServerDomainSocketChannel;
import io.netty.channel.kqueue.KQueueEventLoopGroup;
import io.netty.channel.kqueue.KQueueServerDomainSocketChannel;
import io.netty.channel.unix.DomainSocketAddress;
import picocli.CommandLine;

final class AppEncryptionServer implements Callable<Void> {
  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionServer.class);

  enum MetastoreType { MEMORY, JDBC, DYNAMODB }

  enum KmsType { STATIC, AWS }

  // Options to configure the metastore
  @CommandLine.Option(names = "--metastore-type", defaultValue = "MEMORY",
      description = "Type of metastore to use. Enum values: ${COMPLETION-CANDIDATES}")
  private static MetastoreType metastoreType;
  @CommandLine.Option(names = "--jdbc-url",
      description = "JDBC URL to use for JDBC metastore. Required for JDBC metastore.")
  private static String jdbcUrl;

  // Options to configure the KMS
  @CommandLine.Option(names = "--kms-type", defaultValue = "STATIC",
      description = "Type of key management service to use. Enum values: ${COMPLETION-CANDIDATES}")
  private static KmsType kmsType;
  @CommandLine.Option(names = "--preferred-region",
      description = "Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.")
  private static String preferredRegion;
  @CommandLine.Option(names = "--region-arn-tuples", split = ",",
      description = "Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.")
  private static Map<String, String> regionMap;

  // Options to configure the server
  @CommandLine.Option(names = "--productId", description = "Specify the product id", required = true)
  private static String productId;
  @CommandLine.Option(names = "--serviceId", description = "Specify the service id", required = true)
  private static String serviceId;
  @CommandLine.Option(names = "--uds", description = "Unix domain socket file",
      defaultValue = "/tmp/appencryption.sock", required = true)
  private static String udsFilePath;

  // Options to set up crypto policy
  @CommandLine.Option(names = "--key-expiration-days", defaultValue = "90",
      description = "The number of days after which a key will expire")
  private static int keyExpirationDays;
  @CommandLine.Option(names = "--revoke-check-minutes", defaultValue = "60",
      description = "Sets the cache's TTL to revoke the keys in the cache")
  private static int revokeCheckMinutes;
  @CommandLine.Option(names = "--session-caching", defaultValue = "false",
      description = "Enable/disable the session caching")
  private static boolean sessionCacheEnabled;
  @CommandLine.Option(names = "--session-cache-max-size", defaultValue = "1000",
      description = "Define the number of maximum sessions to cache.")
  private static int sessionCacheMaxSize;
  @CommandLine.Option(names = "--session-cache-expire-minutes", defaultValue = "2",
      description = "Evict the session from cache after some minutes.")
  private static int sessionCacheExpireMinutes;

  private static SessionFactory sessionFactory;

  private AppEncryptionServer() {
  }

  private NettyServerBuilder bindUDS(final String uds) {

    // Create a new server to listen on a socket
    EventLoopGroup group;
    NettyServerBuilder builder = NettyServerBuilder.forAddress(new DomainSocketAddress(uds));
    if (SystemUtils.IS_OS_MAC) {
      group = new KQueueEventLoopGroup();
      builder.channelType(KQueueServerDomainSocketChannel.class);
    }
    else if (SystemUtils.IS_OS_LINUX) {
      group = new EpollEventLoopGroup();
      builder.channelType(EpollServerDomainSocketChannel.class);
    }
    else {
      throw new IllegalStateException("binding to unix:// addresses is only supported on Linux and macOS");
    }
    builder.workerEventLoopGroup(group).bossEventLoopGroup(group);

    return builder;
  }

  private Metastore<JSONObject> setupMetastore() {

    if (metastoreType == MetastoreType.JDBC) {
      if (jdbcUrl != null) {
        logger.info("using JDBC-based metastore...");

        // Setup JDBC persistence from command line argument using Hikari connection pooling
        HikariDataSource dataSource = new HikariDataSource();
        dataSource.setJdbcUrl(jdbcUrl);
        return JdbcMetastoreImpl.newBuilder(dataSource).build();
      }
      else {
        CommandLine.usage(this, System.out);
        return null;
      }
    }
    else if (metastoreType == MetastoreType.DYNAMODB) {
      logger.info("using DynamoDB-based metastore...");

      return DynamoDbMetastoreImpl.newBuilder().build();
    }

    logger.info("using in-memory metastore...");
    return new InMemoryMetastoreImpl<>();
  }

  private CryptoPolicy setupCryptoPolicy() {

    return BasicExpiringCryptoPolicy
        .newBuilder()
        .withKeyExpirationDays(keyExpirationDays)
        .withRevokeCheckMinutes(revokeCheckMinutes)
        .withCanCacheSessions(sessionCacheEnabled)
        .withSessionCacheMaxSize(sessionCacheMaxSize)
        .withSessionCacheExpireMinutes(sessionCacheExpireMinutes)
        .build();
  }

  private KeyManagementService setupKeyManagementService() {

    if (kmsType == KmsType.AWS) {
      if (preferredRegion != null && regionMap != null) {
        logger.info("using AWS KMS...");

        // build the ARN regions including preferred region
        return AwsKeyManagementServiceImpl
          .newBuilder(regionMap, preferredRegion).build();
      }
      else {
        CommandLine.usage(this, System.out);
        return null;
      }
    }

    logger.info("using static KMS...");
    return new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!");
  }

  public static SessionFactory getSessionFactory() {
    return sessionFactory;
  }

  @Override
  public Void call() throws InterruptedException, IOException {

    sessionFactory = SessionFactory
        .newBuilder(productId, serviceId)
        .withMetastore(setupMetastore())
        .withCryptoPolicy(setupCryptoPolicy())
        .withKeyManagementService(setupKeyManagementService())
        .build();

    NettyServerBuilder nettyServerBuilder = bindUDS(udsFilePath);
    Executor executor = MoreExecutors.directExecutor();
    nettyServerBuilder.executor(executor);

    Server server = nettyServerBuilder.addService(new AppEncryptionImpl()).build();

    // Start the server
    server.start();
    logger.info("Server has started");

    Runtime.getRuntime().addShutdownHook(new Thread(() -> {
      server.shutdown();
    }));

    // Don't exit the main thread. Wait until server is terminated.
    server.awaitTermination();
    return null;
  }

  public static void main(final String[] args) {
    CommandLine.call(new AppEncryptionServer(), args);
  }
}

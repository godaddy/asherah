package com.godaddy.asherah.grpc;

import com.google.common.util.concurrent.MoreExecutors;

import java.io.IOException;
import java.util.Locale;
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
import org.apache.commons.lang3.SystemUtils;
import picocli.CommandLine;

public class AppEncryptionServer implements Callable<Void> {
  private static Executor executor;

  enum MetastoreType {MEMORY, JDBC, DYNAMODB}

  enum KmsType {STATIC, AWS}

  // Options to configure the metastore
  @CommandLine.Option(names = "--metastore-type", defaultValue = "MEMORY",
    description = "Type of metastore to use. Enum values: ${COMPLETION-CANDIDATES}")
  static MetastoreType metastoreType;
  @CommandLine.Option(names = "--jdbc-url",
    description = "JDBC URL to use for JDBC metastore. Required for JDBC metastore.")
  static String jdbcUrl;

  // Options to configure the KMS
  @CommandLine.Option(names = "--kms-type", defaultValue = "STATIC",
    description = "Type of key management service to use. Enum values: ${COMPLETION-CANDIDATES}")
  static KmsType kmsType;
  @CommandLine.Option(names = "--preferred-region",
    description = "Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.")
  static String preferredRegion;
  @CommandLine.Option(names = "--region-arn-tuples", split = ",",
    description = "Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.")
  static Map<String, String> regionMap;

  // Options to configure the server
  @CommandLine.Option(names = "--productId", description = "Specify the product id", required = true)
  static String productId;
  @CommandLine.Option(names = "--serviceId", description = "Specify the service id", required = true)
  static String serviceId;
  @CommandLine.Option(names = "--uds", description = "Unix domain socket file",
    defaultValue = "/tmp/appencryption.sock", required = true)
  static String uds;

  // Options to set up crypto policy
  @CommandLine.Option(names = "--key-expiration-days", defaultValue = "90",
    description = "The number of days after which a key will expire")
  static int keyExpirationDays;
  @CommandLine.Option(names = "--revoke-check-minutes", defaultValue = "60",
    description = "Sets the cache's TTL to revoke the keys in the cache")
  static int revokeCheckMinutes;
  @CommandLine.Option(names = "--session-caching", defaultValue = "false",
    description = "Enable/disable the session caching")
  static boolean sessionCacheEnabled;
  @CommandLine.Option(names = "--session-cache-max-size", defaultValue = "1000",
    description = "Define the number of maximum sessions to cache.")
  static int sessionCacheMaxSize;
  @CommandLine.Option(names = "--session-cache-expire-minutes", defaultValue = "2",
    description = "Evict the session from cache after some minutes.")
  static int sessionCacheExpireMinutes;

  private AppEncryptionServer() {
  }

  public static void main(final String[] args) {
    CommandLine.call(new AppEncryptionServer(), args);
  }

  private NettyServerBuilder bindUDS(String uds) {

    // Create a new server to listen on a socket
    NettyServerBuilder builder = NettyServerBuilder.forAddress(new DomainSocketAddress(uds));
    EventLoopGroup group;

    if (SystemUtils.IS_OS_MAC) {
      group = new KQueueEventLoopGroup();
      builder.channelType(KQueueServerDomainSocketChannel.class);
    } else if (SystemUtils.IS_OS_LINUX) {
      group = new EpollEventLoopGroup();
      builder.channelType(EpollServerDomainSocketChannel.class);
    } else {
      throw new IllegalStateException("binding to unix:// addresses is only supported on Linux and macOS");
    }
    builder.workerEventLoopGroup(group).bossEventLoopGroup(group);

    return builder;
  }

  @Override
  public Void call() throws InterruptedException, IOException {

    NettyServerBuilder nettyServerBuilder = bindUDS("/tmp/appencryption.sock");
    executor = MoreExecutors.directExecutor();
    nettyServerBuilder.executor(executor);

    Server server = nettyServerBuilder.addService(new AppEncryptionImpl()).build();

    // Start the server
    server.start();

    System.out.println("Server has started");

    Runtime.getRuntime().addShutdownHook(new Thread(() -> {
      server.shutdown();
    }));

    // Don't exit the main thread. Wait until server is terminated.
    server.awaitTermination();

    return null;
  }
}

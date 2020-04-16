package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import com.google.common.util.concurrent.MoreExecutors;
import io.grpc.ServerBuilder;
import org.apache.commons.lang3.SystemUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.util.concurrent.Executor;
import java.util.concurrent.TimeUnit;

import io.grpc.Server;
import io.grpc.netty.NettyServerBuilder;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.epoll.EpollEventLoopGroup;
import io.netty.channel.epoll.EpollServerDomainSocketChannel;
import io.netty.channel.kqueue.KQueueEventLoopGroup;
import io.netty.channel.kqueue.KQueueServerDomainSocketChannel;
import io.netty.channel.unix.DomainSocketAddress;

class AppEncryptionServer {
  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionServer.class);

  private final Server server;
  private final String udsFilePath;

  /** Create an AppEncryptionServer server listening on default uds path using a {@code sessionFactory}. */
  AppEncryptionServer(final SessionFactory sessionFactory) {
    this(sessionFactory, Constants.DEFAULT_UDS_PATH);
  }

  /** Create an AppEncryptionServer server listening on uds path using a {@code sessionFactory}. */
  AppEncryptionServer(final SessionFactory sessionFactory, final String udsFilePath) {
    this.udsFilePath = udsFilePath;
    NettyServerBuilder nettyServerBuilder = getNettyServerBuilder(udsFilePath);
    Executor executor = MoreExecutors.directExecutor();
    nettyServerBuilder.executor(executor);

    server = nettyServerBuilder.addService(new AppEncryptionImpl(sessionFactory)).build();
  }

  /**
   * Create an AppEncryptionServer server with {@code serverBuilder} listening on {@code udsFilePath}
   * using a {@code sessionFactory}.
   */
  AppEncryptionServer(final SessionFactory sessionFactory, final String udsFilePath,
      final ServerBuilder<?> serverBuilder) {
    this.udsFilePath = udsFilePath;
    server = serverBuilder.addService(new AppEncryptionImpl(sessionFactory)).build();
  }

  /** Start serving requests. */
  public void start() throws IOException {
    server.start();
    logger.info("server has started listening on {}", udsFilePath);

    Runtime.getRuntime().addShutdownHook(new Thread(() -> {
      logger.info("shutting down gRPC server since JVM is shutting down...");
      try {
        AppEncryptionServer.this.stop();
      }
      catch (InterruptedException e) {
        e.printStackTrace();
      }
      logger.info("server shut down");
    }));
  }

  /**
   * Stop serving requests and shutdown resources.
   * Wait for {@code Constants.DEFAULT_SERVER_TIMEOUT} seconds for all pre-existing streams to complete
   */
  public void stop() throws InterruptedException {
    if (server != null) {
      server.shutdown().awaitTermination(Constants.DEFAULT_SERVER_TIMEOUT, TimeUnit.SECONDS);
    }
  }

  /** Await termination on the main thread since the grpc library uses daemon threads. */
  public void blockUntilShutdown() throws InterruptedException {
    if (server != null) {
      server.awaitTermination();
    }
  }

  /** Bind server to {@code uds} based on the operating system. */
  private NettyServerBuilder getNettyServerBuilder(final String uds) {
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
}

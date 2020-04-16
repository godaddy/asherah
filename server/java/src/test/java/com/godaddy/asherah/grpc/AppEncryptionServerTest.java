package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import io.grpc.inprocess.InProcessServerBuilder;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.io.IOException;

import static com.godaddy.asherah.grpc.Constants.DEFAULT_UDS_PATH;
import static org.junit.jupiter.api.Assertions.*;

class AppEncryptionServerTest {

  SessionFactory sessionFactory;

  AppEncryptionServer server;

  @BeforeEach
  void setup() {
    sessionFactory = SessionFactory
      .newBuilder("product", "service")
      .withInMemoryMetastore()
      .withNeverExpiredCryptoPolicy()
      .withStaticKeyManagementService("mysupersecretstaticmasterkey!!!!")
      .build();
  }

  @AfterEach
  void tearDown() throws InterruptedException {
    server.stop();
    sessionFactory.close();
  }

  @Test
  void testConstructorWithSessionFactory() throws IOException {
    server = new AppEncryptionServer(sessionFactory);
    assertNotNull(server);
    server.start();
  }

  @Test
  void testConstructorWithSessionFactoryAndUdsPath() throws IOException {
    server = new AppEncryptionServer(sessionFactory, "/tmp/testserver.sock");
    assertNotNull(server);
    server.start();
  }

  @Test
  void testConsructorWithSessionFactoryAndServerBuilder() throws IOException {
    InProcessServerBuilder inProcessServerBuilder = InProcessServerBuilder.forName("test-server").directExecutor();
    server = new AppEncryptionServer(sessionFactory, DEFAULT_UDS_PATH, inProcessServerBuilder);
    assertNotNull(server);
    server.start();
  }

  @Test
  void testBlockTillShutDown() throws InterruptedException {
    new Thread(() -> {
      server = new AppEncryptionServer(sessionFactory);
      try {
        server.start();
        server.blockUntilShutdown();
      } catch (Exception e) {
        e.printStackTrace();
      }
    }).start();

    // Manually sleep for a while to start the server in a separate thread and wait for the main thread to finish
    Thread.sleep(2000);
  }
}

package com.godaddy.asherah.appencryption;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.noop.NoopMeter;

import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.time.Instant;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

@ExtendWith(MockitoExtension.class)
class SessionFactoryTest {
  @Mock
  Metastore<JSONObject> metastore;
  @Mock
  SecureCryptoKeyMap<Instant> systemKeyCache;
  @Mock
  CryptoPolicy cryptoPolicy;
  @Mock
  KeyManagementService keyManagementService;

  private final static String testPartitionId = "test_partition_id";
  private final static String testServiceId = "test_service_id";
  private final static String testProductId = "test_product_id";
  private final static String testMasterKey = "test_master_key_that_is_32_bytes";

  private SessionFactory sessionFactory;

  @BeforeEach
  void setUp() {
    sessionFactory = new SessionFactory(
        testProductId,
        testServiceId,
        metastore,
        systemKeyCache,
        cryptoPolicy,
        keyManagementService);
  }

  @AfterEach
  void tearDown() {
    sessionFactory.close();
  }

  @Test
  void testConstructor() {
    try (SessionFactory sessionFactory = new SessionFactory(
        testProductId,
        testServiceId,
        metastore,
        systemKeyCache,
        cryptoPolicy,
        keyManagementService)) {
      assertNotNull(sessionFactory);
    }
  }

  @Test
  void testSharedIkCacheSetup() {
    // Test flows around shared IK cache setup, including cache loader and removal flows (via close)
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withUseSharedIntermediateKeyCache(true)
        .build();
    try (SessionFactory sessionFactory = new SessionFactory(
        testProductId,
        testServiceId,
        metastore,
        systemKeyCache,
        policy,
        keyManagementService)) {
      try (Session<byte[], byte[]> session = sessionFactory.getSessionBytes("1234")) {
        assertNotNull(session);
      }
    }
  }

  @Test
  void testSharedIkCacheWithEvictionCheckStillUsed() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withUseSharedIntermediateKeyCache(true)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testMasterKey)
        .build()) {
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};

        byte[] drr = session.encrypt(payload);

        try (Session<byte[], byte[]> session2 = factory.getSessionBytes(testPartitionId)) {
          byte[] decryptedPayload = session2.decrypt(drr);

          assertArrayEquals(payload, decryptedPayload);
        }
      }
    }
  }

  @Test
  void testSharedIkCacheWtihEvictionCheckNotUsedAndAfter() {
    long sharedIkCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withUseSharedIntermediateKeyCache(true)
        .build());
    when(policy.getSharedIkCacheExpireAfterAccessMillis()).thenReturn(sharedIkCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        drr = session.encrypt(payload);

        Thread.sleep(sharedIkCacheExpireMillis * 3);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      System.out.println("JOEY testSharedIkCacheWtihEvictionCheckNotUsedAndAfter done with session 1");

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        Thread.sleep(sharedIkCacheExpireMillis * 30);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      System.out.println("JOEY testSharedIkCacheWtihEvictionCheckNotUsedAndAfter done with session 2, sleeping to trigger expiry");

      try {
        Thread.sleep(sharedIkCacheExpireMillis * 3);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        Thread.sleep(sharedIkCacheExpireMillis * 30);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @Test
  void testSharedIkCacheWithEvictionCheckNotUsedEdgeCase() {
    long sharedIkCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withUseSharedIntermediateKeyCache(true)
        .build());
    when(policy.getSharedIkCacheExpireAfterAccessMillis()).thenReturn(sharedIkCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {

        drr = session.encrypt(payload);
        Thread.sleep(sharedIkCacheExpireMillis - 1);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        Thread.sleep(sharedIkCacheExpireMillis * 3);

        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      try {
        Thread.sleep(sharedIkCacheExpireMillis * 3);
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @Test
  void testSharedIkCacheWithMultiThreadedEvictionCheckStillUsed() {
    long sharedIkCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withUseSharedIntermediateKeyCache(true)
        .build());
    when(policy.getSharedIkCacheExpireAfterAccessMillis()).thenReturn(sharedIkCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testMasterKey)
        .build()) {
      int numThreads = 100;
      int numTasks = numThreads * 100;
      ExecutorService pool = Executors.newFixedThreadPool(numThreads);
      LongAdder tasksCompleted = new LongAdder();
      IntStream.range(0, numTasks)
        .parallel()
        .forEach(i -> {
            pool.submit(() -> {
              try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
                byte[] payload = Integer.toHexString(i).getBytes();

                byte[] drr = session.encrypt(payload);
                Thread.sleep(sharedIkCacheExpireMillis * 3);
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();
              } catch (Exception e) {
                fail(e.getMessage());
              }
            });
        });

      try {
        pool.shutdown();
        pool.awaitTermination(60, TimeUnit.SECONDS);
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
    }
  }

  @Test
  void testGetSessionJson() {
    Session<?,?> session = sessionFactory.getSessionJson(testPartitionId);
    assertNotNull(session);
  }

  @Test
  void testGetSessionBytes() {
    Session<?,?> session = sessionFactory.getSessionBytes(testPartitionId);
    assertNotNull(session);
  }

  @Test
  void testGetSessionJsonAsJson() {
    Session<?,?> session = sessionFactory.getSessionJsonAsJson(testPartitionId);
    assertNotNull(session);
  }

  @Test
  void testGetSessionBytesAsJson() {
    Session<?,?> session = sessionFactory.getSessionBytesAsJson(testPartitionId);
    assertNotNull(session);
  }

  @Test
  void testGetEnvelopeEncryptionBytes() {
    EnvelopeEncryption<?> envelopeEncryption = sessionFactory.getEnvelopeEncryptionBytes(testPartitionId);
    assertNotNull(envelopeEncryption);
  }

  @Test
  void testGetPartitionWithPartitionId() {
    Partition partition =
        sessionFactory.getPartition(testPartitionId);
    assertEquals(testPartitionId, partition.getPartitionId());
    assertEquals(testServiceId, partition.getServiceId());
    assertEquals(testProductId, partition.getProductId());
  }

  @Test
  void testCloseSuccess() {
    sessionFactory.close();

    // Verify proper resources are closed
    verify(systemKeyCache).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(systemKeyCache).close();
    sessionFactory.close();

    // Verify proper resources are closed
    verify(systemKeyCache).close();
  }

  @Test
  void testBuilderPathWithPrebuiltInterfaces() {
    SessionFactory.MetastoreStep metastoreStep =
        SessionFactory.newBuilder(testProductId, testServiceId);
    assertNotNull(metastoreStep);

    SessionFactory.CryptoPolicyStep cryptoPolicyStep = metastoreStep.withInMemoryMetastore();
    assertNotNull(cryptoPolicyStep);

    SessionFactory.KeyManagementServiceStep keyManagementServiceStep =
        cryptoPolicyStep.withNeverExpiredCryptoPolicy();
    assertNotNull(keyManagementServiceStep);

    SessionFactory.BuildStep buildStep =
        keyManagementServiceStep.withStaticKeyManagementService(testMasterKey);
    assertNotNull(buildStep);

    SessionFactory sessionFactory = buildStep.build();
    assertNotNull(sessionFactory);
  }

  @Test
  void testBuilderPathWithSpecifiedInterfaces() {
    SessionFactory.MetastoreStep metastoreStep =
        SessionFactory.newBuilder(testProductId, testServiceId);
    assertNotNull(metastoreStep);

    Metastore<JSONObject> metastore = new InMemoryMetastoreImpl<>();
    SessionFactory.CryptoPolicyStep cryptoPolicyStep =
        metastoreStep.withMetastore(metastore);
    assertNotNull(cryptoPolicyStep);

    CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
    SessionFactory.KeyManagementServiceStep keyManagementServiceStep =
        cryptoPolicyStep.withCryptoPolicy(cryptoPolicy);
    assertNotNull(keyManagementServiceStep);

    KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(testMasterKey);
    SessionFactory.BuildStep buildStep =
        keyManagementServiceStep.withKeyManagementService(keyManagementService);
    assertNotNull(buildStep);

    SessionFactory sessionFactory = buildStep.build();
    assertNotNull(sessionFactory);
  }

  @Test
  void testBuilderPathWithMetricsDisabled() {
    SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .build();
    // Just verify our prefixed metrics result in no-op meters
    assertTrue(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.noop") instanceof NoopMeter);
  }
  @Test
  void testBuilderPathWithMetricsEnabled() {
    SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .withMetricsEnabled()
        .build();
    // Just verify our prefixed metrics do not result in no-op meters
    assertFalse(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.valid") instanceof NoopMeter);
  }
}

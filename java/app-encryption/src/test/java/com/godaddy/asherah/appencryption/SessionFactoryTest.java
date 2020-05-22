package com.godaddy.asherah.appencryption;

import com.github.benmanes.caffeine.cache.Cache;
import com.godaddy.asherah.appencryption.SessionFactory.CachedSession;
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
  Metastore<JSONObject> metastoreMock;
  Metastore<JSONObject> metastoreSpy;
  @Mock
  SecureCryptoKeyMap<Instant> systemKeyCache;
  @Mock
  CryptoPolicy cryptoPolicy;
  @Mock
  KeyManagementService keyManagementService;

  private final static String testPartitionId = "test_partition_id";
  private final static String testServiceId = "test_service_id";
  private final static String testProductId = "test_product_id";
  private final static String testStaticMasterKey = "thisIsAStaticMasterKeyForTesting";

  private SessionFactory sessionFactory;

  @BeforeEach
  void setUp() {
    sessionFactory = new SessionFactory(
        testProductId,
        testServiceId,
        metastoreMock,
        systemKeyCache,
        cryptoPolicy,
        keyManagementService);
    metastoreSpy = spy(new InMemoryMetastoreImpl<>());
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
        metastoreMock,
        systemKeyCache,
        cryptoPolicy,
        keyManagementService)) {
      assertNotNull(sessionFactory);
    }
  }

  @Test
  void testSessionCacheSetupAndClose() {
    // Test flows around session cache setup, including cache loader and removal flows (via close)
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build();
    Cache<String, CachedSession> sessionCache = null;
    try (SessionFactory sessionFactory = new SessionFactory(
        testProductId,
        testServiceId,
        metastoreMock,
        systemKeyCache,
        policy,
        keyManagementService)) {
      sessionCache = sessionFactory.getSessionCache();

      try (Session<byte[], byte[]> session = sessionFactory.getSessionBytes("1234")) {
        assertNotNull(session);
      }
      // Verify nothing evicted yet
      assertTrue(sessionCache.estimatedSize() > 0);
    }
    // Verify closing the factory invalidated and cleaned up entries
    assertTrue(sessionCache.estimatedSize() == 0);
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionWhileStillUsedAndNotExpiredShouldNotEvict() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
        byte[] drr = session.encrypt(payload);

        // Reset so we can examine 2nd session's interactions
        reset(metastoreSpy);

        // Use same partition to get the same cached session while it's still in use
        try (Session<byte[], byte[]> session2 = factory.getSessionBytes(testPartitionId)) {
          byte[] decryptedPayload = session2.decrypt(drr);

          assertArrayEquals(payload, decryptedPayload);

          // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
          verify(metastoreSpy, never()).load(any(), any());
        }
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionWhileStillUsedAndExpiredShouldNotEvict() {
    long sessionCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build());
    when(policy.getSessionCacheExpireMillis()).thenReturn(sessionCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
        byte[] drr = session.encrypt(payload);

        // Sleep to trigger the expiration
        // TODO Consider refactoring the Caffeine usage to allow injecting a ticker if sleeping becomes unreliable
        try {
          Thread.sleep(sessionCacheExpireMillis * 3);
        } catch (Exception e) {
          fail(e.getMessage());
        }

        assertEquals(1, factory.getSessionCache().estimatedSize());
        // Reset so we can examine 2nd session's interactions
        reset(metastoreSpy);

        // Use same partition to get the same cached (but expired) session while it's still in use
        try (Session<byte[], byte[]> session2 = factory.getSessionBytes(testPartitionId)) {
          byte[] decryptedPayload = session2.decrypt(drr);

          assertArrayEquals(payload, decryptedPayload);

          // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
          verify(metastoreSpy, never()).load(any(), any());
        }
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionAfterUseAndNotExpiredShouldNotEvict() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        drr = session.encrypt(payload);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Note we do not sleep

      assertEquals(1, factory.getSessionCache().estimatedSize());
      // Reset so we can examine 2nd session's interactions
      reset(metastoreSpy);

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
        // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
        verify(metastoreSpy, never()).load(any(), any());
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionAfterUseAndExpiredShouldEvict() {
    long sessionCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build());
    when(policy.getSessionCacheExpireMillis()).thenReturn(sessionCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      Partition partition = factory.getPartition(testPartitionId);
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        drr = session.encrypt(payload);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Sleep to trigger the expiration
      // TODO Consider refactoring the Caffeine usage to allow injecting a ticker if sleeping becomes unreliable
      try {
        Thread.sleep(sessionCacheExpireMillis * 3);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Reset so we can examine 2nd session's interactions
      reset(metastoreSpy);

      // This will actually create a new session and the previous one will be removed/closed due to expiry
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
        // metastore should have an interaction in the decrypt flow since the cached session expired
        verify(metastoreSpy).load(eq(partition.getIntermediateKeyId()), any());
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionWithMaxSessionNotReachedShouldNotEvict() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(2)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        drr = session.encrypt(payload);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId + 1)) {
      }

      // Sleep to attempt to trigger eviction on next access if it were going to
      try {
        Thread.sleep(100);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Reset so we can examine final session's interactions
      reset(metastoreSpy);

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
        // we should not hit the metastore since the session should not have been evicted
        verify(metastoreSpy, never()).load(any(), any());
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionWithMaxSessionReachedShouldEvict() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(2)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
      byte[] drr = null;
      Partition partition = factory.getPartition(testPartitionId);
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        drr = session.encrypt(payload);
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Had to add extra cache interactions and a sleep to trigger eviction of main session under test.
      // Likely a detail of the tinyLFU algorithm but didn't dig into it.
      IntStream.range(0, 5).forEach(i -> {
        try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId + i)) {
        }
      });
      try {
        Thread.sleep(100);
      } catch (Exception e) {
        fail(e.getMessage());
      }

      // Reset so we can examine final session's interactions
      reset(metastoreSpy);

      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] decryptedPayload = session.decrypt(drr);

        assertArrayEquals(payload, decryptedPayload);
        // we should have hit the metastore since the session was evicted due to max size
        verify(metastoreSpy).load(eq(partition.getIntermediateKeyId()), any());
      } catch (Exception e) {
        fail(e.getMessage());
      }
    }
  }

  @SuppressWarnings("unchecked")
  @Test
  void testSessionCacheGetSessionWithMaxSessionReachedButStillUsedShouldNotEvict() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(1)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withMetastore(metastoreSpy)
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId)) {
        byte[] payload = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};
        byte[] drr = session.encrypt(payload);

        // Force us to hit the max cache size
        String testPartitionId1 = testPartitionId + "1";
        try (Session<byte[], byte[]> session2 = factory.getSessionBytes(testPartitionId1)) {
          byte[] drr1 = session.encrypt(payload);
          byte[] decryptedPayload = session.decrypt(drr1);

          assertArrayEquals(payload, decryptedPayload);

          // Reset so we can examine final session's interactions
          reset(metastoreSpy);

          // Get same session as the outter-most block since this should force both of the sessions to stay
          try (Session<byte[], byte[]> sessionDup = factory.getSessionBytes(testPartitionId)) {
            byte[] decryptedPayloadDup = sessionDup.decrypt(drr);

            assertArrayEquals(payload, decryptedPayloadDup);
            // we should not hit the metastore since the session should not have been evicted
            verify(metastoreSpy, never()).load(any(), any());
          } catch (Exception e) {
            fail(e.getMessage());
          }
        }
      }
    }
  }

  @Test
  void testSessionCacheMultiThreadedSameSessionNoEviction() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
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
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
      assertEquals(1, factory.getSessionCache().estimatedSize());
    }
  }

  @Test
  void testSessionCacheMultiThreadedDifferentSessionsNoEviction() {
    // Have to limit cache size due to memory constraints in build container
    int numThreads = 10;
    int numTasks = numThreads * 100;
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(numTasks)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      ExecutorService pool = Executors.newFixedThreadPool(numThreads);
      LongAdder tasksCompleted = new LongAdder();
      IntStream.range(0, numTasks)
        .parallel()
        .forEach(i -> {
            pool.submit(() -> {
              try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId + i)) {
                byte[] payload = Integer.toHexString(i).getBytes();
                byte[] drr = session.encrypt(payload);
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
      assertEquals(numTasks, factory.getSessionCache().estimatedSize());
    }
  }

  @Test
  void testSessionCacheMultiThreadedWithMaxSessionReachedSameSession() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(1)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
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
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
      assertEquals(1, factory.getSessionCache().estimatedSize());
    }
  }

  @Test
  void testSessionCacheMultiThreadedWithMaxSessionReachedDifferentSessions() {
    CryptoPolicy policy = BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .withSessionCacheMaxSize(1)
        .build();

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      int numThreads = 100;
      int numTasks = numThreads * 100;
      ExecutorService pool = Executors.newFixedThreadPool(numThreads);
      LongAdder tasksCompleted = new LongAdder();
      IntStream.range(0, numTasks)
        .parallel()
        .forEach(i -> {
            pool.submit(() -> {
              try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId + i)) {
                byte[] payload = Integer.toHexString(i).getBytes();
                byte[] drr = session.encrypt(payload);
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
      // Note we can't reliably assert the estimated size matches (see Cache.estimatedSize documentation). The
      // important part of this test is to just make sure all the operations succeeded properly.
    }
  }

  @Test
  void testSessionCacheMultiThreadedWithExpirationSameSession() {
    long sessionCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build());
    when(policy.getSessionCacheExpireMillis()).thenReturn(sessionCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
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
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();

                // Sleep to ensure this entry gets marked as expired
                // TODO Consider refactoring Caffeine usage to allow injecting a ticker if sleeping becomes unreliable
                Thread.sleep(sessionCacheExpireMillis * 3);
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
      } catch (InterruptedException e) {
        e.printStackTrace();
      }

      assertEquals(numTasks, tasksCompleted.sum());
    }
  }

  @Test
  void testSessionCacheMultiThreadedWithExpirationDifferentSessions() {
    long sessionCacheExpireMillis = 10;
    CryptoPolicy policy = spy(BasicExpiringCryptoPolicy.newBuilder()
        .withKeyExpirationDays(1)
        .withRevokeCheckMinutes(30)
        .withCanCacheSessions(true)
        .build());
    when(policy.getSessionCacheExpireMillis()).thenReturn(sessionCacheExpireMillis);

    try (SessionFactory factory = SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withCryptoPolicy(policy)
        .withStaticKeyManagementService(testStaticMasterKey)
        .build()) {
      int numThreads = 100;
      int numTasks = numThreads * 100;
      ExecutorService pool = Executors.newFixedThreadPool(numThreads);
      LongAdder tasksCompleted = new LongAdder();
      IntStream.range(0, numTasks)
        .parallel()
        .forEach(i -> {
            pool.submit(() -> {
              try (Session<byte[], byte[]> session = factory.getSessionBytes(testPartitionId + i)) {
                byte[] payload = Integer.toHexString(i).getBytes();
                byte[] drr = session.encrypt(payload);
                byte[] decryptedPayload = session.decrypt(drr);

                assertArrayEquals(payload, decryptedPayload);
                tasksCompleted.increment();

                // Sleep to ensure this entry gets marked as expired
                // TODO Consider refactoring Caffeine usage to allow injecting a ticker if sleeping becomes unreliable
                Thread.sleep(sessionCacheExpireMillis * 3);
              } catch (Exception e) {
                e.printStackTrace();
              }
            });
        });

      try {
        pool.shutdown();
        assertTrue(pool.awaitTermination(60, TimeUnit.SECONDS));
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
        keyManagementServiceStep.withStaticKeyManagementService(testStaticMasterKey);
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

    KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(testStaticMasterKey);
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
        .withStaticKeyManagementService(testStaticMasterKey)
        .build();
    // Just verify our prefixed metrics result in no-op meters
    assertTrue(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.noop") instanceof NoopMeter);
  }
  @Test
  void testBuilderPathWithMetricsEnabled() {
    SessionFactory.newBuilder(testProductId, testServiceId)
        .withInMemoryMetastore()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testStaticMasterKey)
        .withMetricsEnabled()
        .build();
    // Just verify our prefixed metrics do not result in no-op meters
    assertFalse(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.valid") instanceof NoopMeter);
  }
}

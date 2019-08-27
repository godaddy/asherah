package com.godaddy.asherah.appencryption;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.keymanagement.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMapFactory;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.noop.NoopMeter;

import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.time.Instant;

@ExtendWith(MockitoExtension.class)
class SessionFactoryTest {
  @Mock
  MetastorePersistence<JSONObject> metastorePersistence;
  @Mock
  SecureCryptoKeyMapFactory<Instant> secureCryptoKeyMapFactory;
  @Mock
  SecureCryptoKeyMap<Instant> systemKeyCache;
  @Mock
  CryptoPolicy cryptoPolicy;
  @Mock
  KeyManagementService keyManagementService;

  private final static String testPartitionId = "test_partition_id";
  private final static String testSystemId = "test_system_id";
  private final static String testProductId = "test_product_id";
  private final static String testMasterKey = "test_master_key";

  private SessionFactory sessionFactory;

  @BeforeEach
  void setUp() {
    when(secureCryptoKeyMapFactory.createSecureCryptoKeyMap()).thenReturn(systemKeyCache);
    sessionFactory = new SessionFactory(
        testProductId,
        testSystemId,
        metastorePersistence,
        secureCryptoKeyMapFactory,
        cryptoPolicy,
        keyManagementService);
  }

  @Test
  void testConstructor() {
    SessionFactory sessionFactory = new SessionFactory(
        testProductId,
        testSystemId,
        metastorePersistence,
        secureCryptoKeyMapFactory,
        cryptoPolicy,
        keyManagementService);
    assertNotNull(sessionFactory);
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
  void getAppEncryptionPartitionWithPartition() {
    Partition partition =
        sessionFactory.getPartition(testPartitionId);
    assertEquals(testPartitionId, partition.getPartitionId());
    assertEquals(testSystemId, partition.getSystemId());
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
        SessionFactory.newBuilder(testProductId, testSystemId);
    assertNotNull(metastoreStep);

    SessionFactory.CryptoPolicyStep cryptoPolicyStep = metastoreStep.withMemoryPersistence();
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
        SessionFactory.newBuilder(testProductId, testSystemId);
    assertNotNull(metastoreStep);

    MetastorePersistence<JSONObject> metastorePersistence = new InMemoryMetastoreImpl<>();
    SessionFactory.CryptoPolicyStep cryptoPolicyStep =
        metastoreStep.withMetastorePersistence(metastorePersistence);
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
    SessionFactory.newBuilder(testProductId, testSystemId)
        .withMemoryPersistence()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .build();
    // Just verify our prefixed metrics result in no-op meters
    assertTrue(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.noop") instanceof NoopMeter);
  }
  @Test
  void testBuilderPathWithMetricsEnabled() {
    SessionFactory.newBuilder(testProductId, testSystemId)
        .withMemoryPersistence()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .withMetricsEnabled()
        .build();
    // Just verify our prefixed metrics do not result in no-op meters
    assertFalse(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.valid") instanceof NoopMeter);
  }
}

package com.godaddy.asherah.appencryption;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.keymanagement.KeyManagementService;
import com.godaddy.asherah.appencryption.keymanagement.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.MemoryPersistenceImpl;
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
class AppEncryptionSessionFactoryTest {
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

  private AppEncryptionSessionFactory appEncryptionSessionFactory;

  @BeforeEach
  void setUp() {
    when(secureCryptoKeyMapFactory.createSecureCryptoKeyMap()).thenReturn(systemKeyCache);
    appEncryptionSessionFactory = new AppEncryptionSessionFactory(
        testProductId,
        testSystemId,
        metastorePersistence,
        secureCryptoKeyMapFactory,
        cryptoPolicy,
        keyManagementService);
  }

  @Test
  void testConstructor() {
    AppEncryptionSessionFactory appEncryptionSessionFactory = new AppEncryptionSessionFactory(
        testProductId,
        testSystemId,
        metastorePersistence,
        secureCryptoKeyMapFactory,
        cryptoPolicy,
        keyManagementService);
    assertNotNull(appEncryptionSessionFactory);
  }

  @Test
  void testGetAppEncryptionJson() {
    AppEncryption<?,?> appEncryption = appEncryptionSessionFactory.getAppEncryptionJson(testPartitionId);
    assertNotNull(appEncryption);
  }

  @Test
  void testGetAppEncryptionBytes() {
    AppEncryption<?,?> appEncryption = appEncryptionSessionFactory.getAppEncryptionBytes(testPartitionId);
    assertNotNull(appEncryption);
  }

  @Test
  void testGetAppEncryptionJsonAsJson() {
    AppEncryption<?,?> appEncryption = appEncryptionSessionFactory.getAppEncryptionJsonAsJson(testPartitionId);
    assertNotNull(appEncryption);
  }

  @Test
  void testGetAppEncryptionBytesAsJson() {
    AppEncryption<?,?> appEncryption = appEncryptionSessionFactory.getAppEncryptionBytesAsJson(testPartitionId);
    assertNotNull(appEncryption);
  }

  @Test
  void testGetEnvelopeEncryptionBytes() {
    EnvelopeEncryption<?> envelopeEncryption = appEncryptionSessionFactory.getEnvelopeEncryptionBytes(testPartitionId);
    assertNotNull(envelopeEncryption);
  }

  @Test
  void getAppEncryptionPartitionWithPartition() {
    AppEncryptionPartition appEncryptionPartition =
        appEncryptionSessionFactory.getAppEncryptionPartition(testPartitionId);
    assertEquals(testPartitionId, appEncryptionPartition.getPartitionId());
    assertEquals(testSystemId, appEncryptionPartition.getSystemId());
    assertEquals(testProductId, appEncryptionPartition.getProductId());
  }

  @Test
  void testCloseSuccess() {
    appEncryptionSessionFactory.close();
    
    // Verify proper resources are closed
    verify(systemKeyCache).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(systemKeyCache).close();
    appEncryptionSessionFactory.close();
    
    // Verify proper resources are closed
    verify(systemKeyCache).close();
  }

  @Test
  void testBuilderPathWithPrebuiltInterfaces() {
    AppEncryptionSessionFactory.MetastoreStep metastoreStep =
        AppEncryptionSessionFactory.newBuilder(testProductId, testSystemId);
    assertNotNull(metastoreStep);

    AppEncryptionSessionFactory.CryptoPolicyStep cryptoPolicyStep = metastoreStep.withMemoryPersistence();
    assertNotNull(cryptoPolicyStep);

    AppEncryptionSessionFactory.KeyManagementServiceStep keyManagementServiceStep =
        cryptoPolicyStep.withNeverExpiredCryptoPolicy();
    assertNotNull(keyManagementServiceStep);

    AppEncryptionSessionFactory.BuildStep buildStep =
        keyManagementServiceStep.withStaticKeyManagementService(testMasterKey);
    assertNotNull(buildStep);

    AppEncryptionSessionFactory sessionFactory = buildStep.build();
    assertNotNull(sessionFactory);
  }

  @Test
  void testBuilderPathWithSpecifiedInterfaces() {
    AppEncryptionSessionFactory.MetastoreStep metastoreStep =
        AppEncryptionSessionFactory.newBuilder(testProductId, testSystemId);
    assertNotNull(metastoreStep);

    MetastorePersistence<JSONObject> metastorePersistence = new MemoryPersistenceImpl<>();
    AppEncryptionSessionFactory.CryptoPolicyStep cryptoPolicyStep =
        metastoreStep.withMetastorePersistence(metastorePersistence);
    assertNotNull(cryptoPolicyStep);

    CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
    AppEncryptionSessionFactory.KeyManagementServiceStep keyManagementServiceStep =
        cryptoPolicyStep.withCryptoPolicy(cryptoPolicy);
    assertNotNull(keyManagementServiceStep);

    KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(testMasterKey);
    AppEncryptionSessionFactory.BuildStep buildStep =
        keyManagementServiceStep.withKeyManagementService(keyManagementService);
    assertNotNull(buildStep);

    AppEncryptionSessionFactory sessionFactory = buildStep.build();
    assertNotNull(sessionFactory);
  }

  @Test
  void testBuilderPathWithMetricsDisabled() {
    AppEncryptionSessionFactory.newBuilder(testProductId, testSystemId)
        .withMemoryPersistence()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .build();
    // Just verify our prefixed metrics result in no-op meters
    assertTrue(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.noop") instanceof NoopMeter);
  }
  @Test
  void testBuilderPathWithMetricsEnabled() {
    AppEncryptionSessionFactory.newBuilder(testProductId, testSystemId)
        .withMemoryPersistence()
        .withNeverExpiredCryptoPolicy()
        .withStaticKeyManagementService(testMasterKey)
        .withMetricsEnabled()
        .build();
    // Just verify our prefixed metrics do not result in no-op meters
    assertFalse(Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".should.be.valid") instanceof NoopMeter);
  }
}

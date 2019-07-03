package com.godaddy.asherah.testapp.multithreaded;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver.ConfigurationParameter;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;
import com.godaddy.asherah.testapp.utils.PersistenceFactory;
import com.godaddy.asherah.testapp.utils.SessionFactoryGenerator;

import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

@ExtendWith(ConfigurationParameterResolver.class)
class MultiPartitionMultiThreadedTest {
  private static final Logger LOG = LoggerFactory.getLogger(MultiPartitionMultiThreadedTest.class);

  private static Persistence<byte[]> persistenceBytes;

  private AppEncryptionSessionFactory appEncryptionSessionFactory;

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setUp() {
    appEncryptionSessionFactory = SessionFactoryGenerator.createDefaultAppEncryptionSessionFactory();
  }

  @AfterEach
  public void tearDown() {
    appEncryptionSessionFactory.close();
  }

  /**
   * Create a single session to encrypt and decrypt data for multiple partitions.
   * <p>
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedSameFactoryMultiplePartitionsEncryptDecrypt(
      @ConfigurationParameter(TEST_PARAM_NUM_ITERATIONS) final int numIterations,
      @ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads,
      @ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes,
      @ConfigurationParameter(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) final long threadPoolTimeoutSeconds) {
    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, numThreads)
        .parallel()
        .forEach(i -> {
          pool.submit(() -> {
            runEncryptDecryptTest(numIterations, String.format("thread-pool-%d", i), payloadSizeBytes);
            tasksCompleted.increment();
          });
        });

    try {
      pool.shutdown();
      pool.awaitTermination(threadPoolTimeoutSeconds, TimeUnit.SECONDS);
    }
    catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }

    assertEquals(numThreads, tasksCompleted.intValue());
  }


  /**
   * Create a single session to store and load data for multiple partitions.
   * <p>
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedSameFactoryMultiplePartitionsLoadStore(
      @ConfigurationParameter(TEST_PARAM_NUM_ITERATIONS) final int numIterations,
      @ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads,
      @ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes,
      @ConfigurationParameter(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) final long threadPoolTimeoutSeconds) {
    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, numThreads)
        .parallel()
        .forEach(i -> {
          pool.submit(() -> {
            runLoadStoreTest(numIterations, String.format("thread-pool-%d", i), payloadSizeBytes);
            tasksCompleted.increment();
          });
        });

    try {
      pool.shutdown();
      pool.awaitTermination(threadPoolTimeoutSeconds, TimeUnit.SECONDS);
    }
    catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }
    assertEquals(numThreads, tasksCompleted.intValue());
  }

  private void runEncryptDecryptTest(final int testIterations, final String partitionId, final int payloadSizeBytesBase) {
    try (AppEncryption<JSONObject, byte[]> partition = appEncryptionSessionFactory.getAppEncryptionJson(partitionId)) {
      Map<String, byte[]> dataStore = new HashMap<>();

      String partitionPart = "partition-" + partitionId + "-";

      for (int i = 0; i < testIterations; i++) {
        // Note the size will be slightly larger since we're adding extra unique meta
        JSONObject jsonObject = PayloadGenerator.createRandomJsonPayload(payloadSizeBytesBase);
        String keyPart = String.format("iteration-%d", i);
        jsonObject.put("payload", partitionPart + keyPart);

        dataStore.put(keyPart, partition.encrypt(jsonObject));
      }

      dataStore.forEach((key, value) -> {
        JSONObject decryptedObject = partition.decrypt(value);
        assertEquals(partitionPart + key, decryptedObject.get("payload"));
      });
    }
    catch (Exception e) {
      LOG.error("unexpected error during call", e);
      throw e;
    }
  }

  private void runLoadStoreTest(final int testIterations, final String partitionId, final int payloadSizeBytesBase) {
    try (AppEncryption<JSONObject, byte[]> partition = appEncryptionSessionFactory.getAppEncryptionJson(partitionId)) {
      String partitionPart = "partition-" + partitionId + "-";

      for (int i = 0; i < testIterations; i++) {
        // Note the size will be slightly larger since we're adding extra unique meta
        JSONObject jsonObject = PayloadGenerator.createRandomJsonPayload(payloadSizeBytesBase);
        String keyPart = String.format("iteration-%d", i);
        jsonObject.put("payload", partitionPart + keyPart);

        String persistenceKey = partition.store(jsonObject, persistenceBytes);
        Optional<JSONObject> decryptedJsonPayload = partition.load(persistenceKey, persistenceBytes);
        if (decryptedJsonPayload.isPresent()) {
          assertEquals(partitionPart + keyPart, decryptedJsonPayload.get().get("payload"));
        }
        else {
          fail("Json load did not return decrypted payload");
        }
      }
    }
    catch (Exception e) {
      LOG.error("unexpected error during call", e);
      throw e;
    }
  }
}


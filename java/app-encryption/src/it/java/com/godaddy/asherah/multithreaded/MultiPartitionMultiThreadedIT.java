package com.godaddy.asherah.multithreaded;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.PersistenceFactory;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
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

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

class MultiPartitionMultiThreadedIT {

  private static final Logger LOG = LoggerFactory.getLogger(MultiPartitionMultiThreadedIT.class);

  private static Persistence<byte[]> persistenceBytes;

  private SessionFactory sessionFactory;

  @BeforeAll
  public static void init() {
    persistenceBytes = PersistenceFactory.<byte[]>createInMemoryPersistence();
  }

  @BeforeEach
  public void setUp() {
    sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(),
      TestSetup.createMetastore());
  }

  @AfterEach
  public void tearDown() {
    sessionFactory.close();
  }

  /**
   * Create a single session to encrypt and decrypt data for multiple partitions.
   * <p>
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedSameFactoryMultiplePartitionsEncryptDecrypt() {
    ExecutorService pool = Executors.newFixedThreadPool(TEST_PARAM_NUM_THREADS);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, TEST_PARAM_NUM_THREADS)
      .parallel()
      .forEach(i -> {
        pool.submit(() -> {
          runEncryptDecryptTest(TEST_PARAM_NUM_ITERATIONS, String.format("thread-pool-%d", i), TEST_PARAM_PAYLOAD_SIZE_BYTES);
          tasksCompleted.increment();
        });
      });

    try {
      pool.shutdown();
      pool.awaitTermination(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS, TimeUnit.SECONDS);
    }
    catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }

    assertEquals(TEST_PARAM_NUM_THREADS, tasksCompleted.intValue());
  }

  /**
   * Create a single session to store and load data for multiple partitions.
   * <p>
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedSameFactoryMultiplePartitionsLoadStore() {
    ExecutorService pool = Executors.newFixedThreadPool(TEST_PARAM_NUM_THREADS);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, TEST_PARAM_NUM_THREADS)
      .parallel()
      .forEach(i -> {
        pool.submit(() -> {
          runLoadStoreTest(TEST_PARAM_NUM_ITERATIONS, String.format("thread-pool-%d", i), TEST_PARAM_PAYLOAD_SIZE_BYTES);
          tasksCompleted.increment();
        });
      });

    try {
      pool.shutdown();
      pool.awaitTermination(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS, TimeUnit.SECONDS);
    }
    catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }
    assertEquals(TEST_PARAM_NUM_THREADS, tasksCompleted.intValue());
  }

  private void runEncryptDecryptTest(final int testIterations, final String partitionId, final int payloadSizeBytesBase) {
    try (Session<JSONObject, byte[]> session = sessionFactory.getSessionJson(partitionId)) {
      Map<String, byte[]> dataStore = new HashMap<>();

      String partitionPart = "partition-" + partitionId + "-";

      for (int i = 0; i < testIterations; i++) {
        // Note the size will be slightly larger since we're adding extra unique meta
        JSONObject jsonObject = PayloadGenerator.createRandomJsonPayload(payloadSizeBytesBase);
        String keyPart = String.format("iteration-%d", i);
        jsonObject.put("payload", partitionPart + keyPart);

        dataStore.put(keyPart, session.encrypt(jsonObject));
      }

      dataStore.forEach((key, value) -> {
        JSONObject decryptedObject = session.decrypt(value);
        assertEquals(partitionPart + key, decryptedObject.get("payload"));
      });
    }
    catch (Exception e) {
      LOG.error("unexpected error during call", e);
      throw e;
    }
  }

  private void runLoadStoreTest(final int testIterations, final String partitionId, final int payloadSizeBytesBase) {
    try (Session<JSONObject, byte[]> session = sessionFactory.getSessionJson(partitionId)) {
      String partitionPart = "partition-" + partitionId + "-";

      for (int i = 0; i < testIterations; i++) {
        // Note the size will be slightly larger since we're adding extra unique meta
        JSONObject jsonObject = PayloadGenerator.createRandomJsonPayload(payloadSizeBytesBase);
        String keyPart = String.format("iteration-%d", i);
        jsonObject.put("payload", partitionPart + keyPart);

        String persistenceKey = session.store(jsonObject, persistenceBytes);
        Optional<JSONObject> decryptedJsonPayload = session.load(persistenceKey, persistenceBytes);
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

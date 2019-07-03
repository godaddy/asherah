package com.godaddy.asherah.testapp.multithreaded;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver.ConfigurationParameter;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;
import com.godaddy.asherah.testapp.utils.SessionFactoryGenerator;

import org.json.JSONObject;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

@ExtendWith(ConfigurationParameterResolver.class)
class MultiFactoryThreadedTest {
  private static final Logger LOG = LoggerFactory.getLogger(MultiFactoryThreadedTest.class);

  private void runPartitionTest(final int testIterations, final String partitionId, final int payloadSizeBytesBase) {
    try (AppEncryptionSessionFactory factory = SessionFactoryGenerator.createDefaultAppEncryptionSessionFactory()) {
      try (AppEncryption<JSONObject, byte[]> partition = factory.getAppEncryptionJson(partitionId)) {
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
    }
    catch (Exception e) {
      LOG.error("unexpected error during call", e);
      throw e;
    }
  }

  /**
   * Create multiple sessions from multiple factories
   * to encrypt and decrypt data for multiple partitions.
   *
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedMultiFactoryUniquePartitionsEncryptDecrypt(
      @ConfigurationParameter(TEST_PARAM_NUM_ITERATIONS) final int numIterations,
      @ConfigurationParameter(TEST_PARAM_NUM_REQUESTS) final int numRequests,
      @ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads,
      @ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes,
      @ConfigurationParameter(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) final long threadPoolTimeoutSeconds) {
    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, numRequests)
            .parallel()
            .forEach(i -> {
              pool.submit(() -> {
                runPartitionTest(numIterations, String.format("request-%d", i), payloadSizeBytes);
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

    assertEquals(numRequests, tasksCompleted.intValue());
  }

  /**
   * Using the same partition id, create multiple sessions
   * from multiple factories to encrypt and decrypt data.
   *
   * Ensure keys get created properly, DRR's are created as expected,
   * and (future) collect timing information to ensure no thread is
   * starved more than others since there will be some locks in play.
   */
  @Test
  void multiThreadedMultiFactorySamePartitionEncryptDecrypt(
      @ConfigurationParameter(TEST_PARAM_NUM_ITERATIONS) final int numIterations,
      @ConfigurationParameter(TEST_PARAM_NUM_REQUESTS) final int numRequests,
      @ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads,
      @ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes,
      @ConfigurationParameter(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) final long threadPoolTimeoutSeconds) {
    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();

    IntStream.range(0, numRequests)
        .parallel()
        .forEach(i -> {
          pool.submit(() -> {
            runPartitionTest(numIterations, DEFAULT_PARTITION_ID, payloadSizeBytes);
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

    assertEquals(numRequests, tasksCompleted.intValue());
  }

}

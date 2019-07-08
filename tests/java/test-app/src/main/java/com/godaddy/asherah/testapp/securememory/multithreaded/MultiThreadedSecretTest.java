package com.godaddy.asherah.testapp.securememory.multithreaded;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecretFactory;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver.ConfigurationParameter;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

@ExtendWith(ConfigurationParameterResolver.class)
class MultiThreadedSecretTest {
  private static final Logger LOG = LoggerFactory.getLogger(MultiThreadedSecretTest.class);

  private byte[] payload;

  @BeforeEach
  public void setupTest(@ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes) {
    payload = PayloadGenerator.createRandomBytePayload(payloadSizeBytes);
  }

  @Test
  void multiThreadedWithSecretBytesAccess(
      @ConfigurationParameter(TEST_PARAM_NUM_REQUESTS) final int numRequests,
      @ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads,
      @ConfigurationParameter(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) final long threadPoolTimeoutSeconds) {
    SecretFactory secretFactory = new ProtectedMemorySecretFactory();
    Secret secret = secretFactory.createSecret(payload.clone());

    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();
    IntStream.range(0, numRequests)
        .parallel()
        .forEach(i -> pool.submit(() -> {
          try {
            secret.withSecretBytes(decryptedBytes -> {
              assertArrayEquals(payload, decryptedBytes);
              tasksCompleted.increment();
            });
          }
          catch (Exception e) {
            LOG.error("unexpected error during call", e);
            throw e;
          }
        }));

    try {
      pool.shutdown();
      pool.awaitTermination(threadPoolTimeoutSeconds, TimeUnit.SECONDS);
    }
    catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }
    assertEquals(numRequests, tasksCompleted.sum());
  }
}

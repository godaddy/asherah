package com.godaddy.asherah.securememory.multithreaded;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecretFactory;
import com.godaddy.asherah.utils.PayloadGenerator;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

class MultiThreadedSecretIT {
  private static final Logger LOG = LoggerFactory.getLogger(MultiThreadedSecretIT.class);

  private byte[] payload;

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createRandomBytePayload(TEST_PARAM_PAYLOAD_SIZE_BYTES);
  }

  @Test
  void multiThreadedWithSecretBytesAccess() {
    SecretFactory secretFactory = new ProtectedMemorySecretFactory();
    Secret secret = secretFactory.createSecret(payload.clone());

    ExecutorService pool = Executors.newFixedThreadPool(TEST_PARAM_NUM_THREADS);
    LongAdder tasksCompleted = new LongAdder();
    IntStream.range(0, TEST_PARAM_NUM_REQUESTS)
      .parallel()
      .forEach(i -> pool.submit(() -> {
        try {
          secret.withSecretBytes(decryptedBytes -> {
            assertArrayEquals(payload, decryptedBytes);
            tasksCompleted.increment();
          });
        } catch (Exception e) {
          LOG.error("unexpected error during call", e);
          throw e;
        }
      }));

    try {
      pool.shutdown();
      pool.awaitTermination(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS, TimeUnit.SECONDS);
    } catch (InterruptedException e) {
      LOG.error("interrupted exception during thread pool shutdown", e);
    }
    assertEquals(TEST_PARAM_NUM_REQUESTS, tasksCompleted.sum());
  }
}

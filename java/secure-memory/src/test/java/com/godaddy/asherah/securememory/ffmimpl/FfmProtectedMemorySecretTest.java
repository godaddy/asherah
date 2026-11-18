package com.godaddy.asherah.securememory.ffmimpl;

import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledForJreRange;
import org.junit.jupiter.api.condition.JRE;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;

/**
 * Tests for FFM-based protected memory secret implementation.
 * These tests only run on Java 22+ where FFM is available.
 */
@EnabledForJreRange(min = JRE.JAVA_22)
class FfmProtectedMemorySecretTest {

  private SecretFactory ffmSecretFactory;

  @BeforeEach
  void setUp() throws Exception {
    // Use reflection to create FFM factory since it's only available on Java 22+
    Class<?> factoryClass = Class.forName("com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory");
    ffmSecretFactory = (SecretFactory) factoryClass.getDeclaredConstructor().newInstance();
  }

  @Test
  void testWithSecretBytesSuccess() {
    byte[] secretBytes = new byte[] {0, 1, 2, 3};
    // Clone to avoid original getting zeroed out for assert purposes
    Secret secret = ffmSecretFactory.createSecret(secretBytes.clone());
    try {
      secret.withSecretBytes(decryptedBytes -> {
        assertArrayEquals(secretBytes, decryptedBytes);
        return null;
      });
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testWithSecretBytesWithClosedSecretShouldFail() {
    byte[] secretBytes = new byte[] {0, 1};
    Secret secret = ffmSecretFactory.createSecret(secretBytes.clone());

    // Close the secret
    secret.close();

    assertThrows(IllegalStateException.class, () -> secret.withSecretBytes(decryptedBytes -> null));
  }

  @Test
  void testWithSecretBytesMultiThreadedAccess() {
    byte[] secretBytes = new byte[] {0, 1, 2, 3};
    Secret secret = ffmSecretFactory.createSecret(secretBytes.clone());

    try {
      // Submit large number of tasks to verify concurrency semantics
      int numThreads = 100;
      int numTasks = numThreads * 1000;
      ExecutorService pool = Executors.newFixedThreadPool(numThreads);
      LongAdder tasksCompleted = new LongAdder();

      IntStream.range(0, numTasks)
          .parallel()
          .forEach(i -> {
            pool.submit(() -> {
              secret.withSecretBytes(decryptedBytes -> {
                assertArrayEquals(secretBytes, decryptedBytes);
                tasksCompleted.increment();
                return null;
              });
            });
          });

      try {
        pool.shutdown();
        pool.awaitTermination(30, TimeUnit.SECONDS);
      }
      catch (InterruptedException e) {
        Thread.currentThread().interrupt();
      }

      assertEquals(numTasks, tasksCompleted.sum());
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testWithSecretUtf8CharsSuccess() {
    char[] secretChars = new char[] {'a', 'b', 'c'};
    Secret secret = ffmSecretFactory.createSecret(secretChars.clone());
    try {
      secret.withSecretUtf8Chars(decryptedChars -> {
        assertArrayEquals(secretChars, decryptedChars);
        return null;
      });
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testWithSecretUtf8CharsWithClosedSecretShouldFail() {
    char[] secretChars = new char[] {'a', 'b'};
    Secret secret = ffmSecretFactory.createSecret(secretChars.clone());

    // Close the secret
    secret.close();

    assertThrows(IllegalStateException.class, () -> secret.withSecretUtf8Chars(decryptedChars -> null));
  }

  @Test
  void testCopySecret() {
    byte[] secretBytes = new byte[] {0, 1, 2, 3};
    Secret secret = ffmSecretFactory.createSecret(secretBytes.clone());
    try {
      Secret secretCopy = secret.copySecret();
      try {
        secretCopy.withSecretBytes(decryptedBytes -> {
          assertArrayEquals(secretBytes, decryptedBytes);
          return null;
        });
      }
      finally {
        secretCopy.close();
      }
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testCloseWithClosedSecretShouldNoop() {
    Secret secret = ffmSecretFactory.createSecret(new byte[] {0, 1});

    // Close twice should not throw
    secret.close();
    secret.close();
    // If we get here without exception, the test passes
  }

  @Test
  void testConstructorWithValidDataSucceeds() {
    // This test verifies that allocation with valid data works
    byte[] secretBytes = new byte[] {1, 2, 3, 4};
    Secret secret = ffmSecretFactory.createSecret(secretBytes);
    assertNotNull(secret);
    secret.close();
  }

  @Test
  void testSecretDataIsZeroedAfterCreation() {
    byte[] originalBytes = new byte[] {1, 2, 3, 4, 5};
    byte[] secretBytes = originalBytes.clone();

    Secret secret = ffmSecretFactory.createSecret(secretBytes);
    try {
      // Original source bytes should be zeroed after secret creation
      byte[] expectedZeroed = new byte[originalBytes.length];
      assertArrayEquals(expectedZeroed, secretBytes, "Source bytes should be zeroed after secret creation");

      // But secret should still contain original data
      secret.withSecretBytes(decryptedBytes -> {
        assertArrayEquals(originalBytes, decryptedBytes);
        return null;
      });
    }
    finally {
      secret.close();
    }
  }
}

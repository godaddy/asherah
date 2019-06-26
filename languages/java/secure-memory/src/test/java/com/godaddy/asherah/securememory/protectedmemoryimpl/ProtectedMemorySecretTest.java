package com.godaddy.asherah.securememory.protectedmemoryimpl;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.LongAdder;
import java.util.stream.IntStream;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxProtectedMemoryAllocator;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSProtectedMemoryAllocator;
import com.sun.jna.Platform;

@ExtendWith(MockitoExtension.class)
class ProtectedMemorySecretTest {
  ProtectedMemoryAllocator protectedMemoryAllocator = null;

  @BeforeEach
  void setUp() {
    if (Platform.isMac()) {
      protectedMemoryAllocator = spy(new MacOSProtectedMemoryAllocator(new MacOSLibc()));
    }
    else if (Platform.isLinux()) {
      protectedMemoryAllocator = spy(new LinuxProtectedMemoryAllocator(new LinuxLibc()));
    }
  }

  @Test
  void testConstructorWithAllocatorReturnsNullShouldFail() {
    doReturn(null).when(protectedMemoryAllocator).alloc(anyLong());
    assertThrows(ProtectedMemoryAllocationFailed.class,
        () -> new ProtectedMemorySecret(new byte[]{0, 1}, protectedMemoryAllocator));
  }

  @Test
  void testWithSecretBytesSuccess() {
    byte[] secretBytes = new byte[]{0, 1};
    // clone to avoid original getting zero'ed out for assert purposes
    try(ProtectedMemorySecret secret = new ProtectedMemorySecret(secretBytes.clone(), protectedMemoryAllocator)) {
      secret.withSecretBytes(decryptedBytes -> {
        assertArrayEquals(secretBytes, decryptedBytes);
        return null;
      });
    }
  }

  @Test
  void testWithSecretBytesWithClosedSecretShouldFail() {
    byte[] secretBytes = new byte[]{0, 1};
    // clone to avoid original getting zero'ed out for assert purposes
    ProtectedMemorySecret secret = new ProtectedMemorySecret(secretBytes.clone(), protectedMemoryAllocator);
    secret.close();
    assertThrows(IllegalStateException.class, () -> secret.withSecretBytes(decryptedBytes -> {
      return null;
    }));
  }

  // TODO Borderline integration test, but still runs fast. Consider moving out or meh?
  @Test
  void testWithSecretBytesMultiThreadedAccess() {
    SecretFactory secretFactory = new ProtectedMemorySecretFactory();
    byte[] secretBytes = new byte[] {0, 1, 2, 3};
    Secret secret = secretFactory.createSecret(secretBytes);

    // Submit large number of tasks to a reasonably sized thread pool to verify concurrency
    // semantics around the protected memory management
    int numThreads = 100;
    int numTasks = numThreads * 1000;
    ExecutorService pool = Executors.newFixedThreadPool(numThreads);
    LongAdder tasksCompleted = new LongAdder();
    IntStream.range(0, numTasks)
      .parallel()
      .forEach(i -> {
          pool.submit(() -> {
            secret.withSecretBytes(decryptedBytes -> {
              tasksCompleted.increment();
              // For some reason assert has to execute last or it skips any other statements
              assertArrayEquals(secretBytes, decryptedBytes);
            });
          });
      });

    try {
      pool.shutdown();
      pool.awaitTermination(30, TimeUnit.SECONDS);
    } catch (InterruptedException e) {
      e.printStackTrace();
    }

    assertEquals(numTasks, tasksCompleted.sum());
  }

  @Test
  void testWithSecretUtf8CharsSuccess() {
    char[] secretChars = new char[]{'a', 'b'};
    try(ProtectedMemorySecret secret = ProtectedMemorySecret.fromCharArray(secretChars, protectedMemoryAllocator)) {
      secret.withSecretUtf8Chars(decryptedChars -> {
        assertArrayEquals(secretChars, decryptedChars);
        return null;
      });
    }
  }

  @Test
  void testWithSecretUtf8CharsWithClosedSecretShouldFail() {
    char[] secretChars = new char[]{'a', 'b'};
    ProtectedMemorySecret secret = ProtectedMemorySecret.fromCharArray(secretChars, protectedMemoryAllocator);
    secret.close();
    assertThrows(IllegalStateException.class, () -> secret.withSecretUtf8Chars(decryptedChars -> {
      return null;
    }));
  }

  @Test
  void testCopySecret() {
    byte[] secretBytes = new byte[]{0, 1};
    // clone to avoid original getting zero'ed out for assert purposes
    try(ProtectedMemorySecret secret = new ProtectedMemorySecret(secretBytes.clone(), protectedMemoryAllocator)) {
      try(ProtectedMemorySecret secretCopy = (ProtectedMemorySecret) secret.copySecret()) {
        secretCopy.withSecretBytes(decryptedBytes -> {
          assertArrayEquals(secretBytes, decryptedBytes);
        });
      }
    }
  }

  @Test
  void testCloseWithClosedSecretShouldNoop() {
    ProtectedMemorySecret secret = new ProtectedMemorySecret(new byte[]{0, 1}, protectedMemoryAllocator);
    secret.close();
    secret.close();
    // verify only called once
    verify(protectedMemoryAllocator).free(any(), anyLong());
  }
}

package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Objects;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.Debug;
import com.godaddy.asherah.securememory.Secret;

/**
 * FFM-based implementation of {@link Secret} backed by mmap'd, mlock'd, mprotect'd memory.
 *
 * <p>The secret is stored in a private off-heap segment that is normally mprotect'd to no-access.
 * Read access is enabled only for the duration of a {@link #withSecretBytes} /
 * {@link #withSecretUtf8Chars} callback, and is reference-counted so that concurrent callers do
 * not race on access-flag transitions.
 *
 * <p>Requires Java 22+.
 */
public class FfmProtectedMemorySecret implements Secret, AutoCloseable {
  private static final Logger LOG = LoggerFactory.getLogger(FfmProtectedMemorySecret.class);
  private static final Charset UTF8_CHARSET = StandardCharsets.UTF_8;

  private final FfmAllocator allocator;
  private final int length;

  /**
   * The protected segment, or {@code null} after close. Mutated only under {@link #accessLock}.
   * Marked volatile so the disposed-state probe in {@link #withSecretBytes} sees the publish.
   */
  private volatile MemorySegment segment;

  /**
   * Counts in-flight readers. Guarded by {@link #accessLock} for both reads and writes (no
   * volatile/atomic). The lock is fair to avoid starvation under bursty access.
   */
  private long accessCounter = 0;
  private final ReentrantLock accessLock = new ReentrantLock(true);

  /** Signaled when {@link #accessCounter} hits 0 so {@link #close()} can wait readers out. */
  private final Condition noReaders = accessLock.newCondition();

  /**
   * Creates a new FFM-based protected memory secret from a byte array.
   *
   * @param sourceBytes the secret bytes (will be securely zeroed after the copy)
   * @param allocator the FFM allocator to use
   */
  FfmProtectedMemorySecret(final byte[] sourceBytes, final FfmAllocator allocator) {
    Objects.requireNonNull(sourceBytes, "sourceBytes");
    Objects.requireNonNull(allocator, "allocator");
    this.allocator = allocator;
    this.length = sourceBytes.length;
    this.segment = allocator.alloc(length);

    if (segment == null || segment.equals(MemorySegment.NULL)) {
      throw new FfmAllocationFailed("Protected memory allocation failed");
    }
    if (Debug.ON) {
      LOG.debug("FFM allocated: {} bytes at address {}", length, segment.address());
    }

    MemorySegment.copy(sourceBytes, 0, segment, ValueLayout.JAVA_BYTE, 0, length);
    allocator.setNoAccess(segment, length);

    // Only zero the caller's buffer once everything else has succeeded.
    secureZeroMemory(sourceBytes);
  }

  /**
   * Creates a new FFM-based protected memory secret from a char array.
   *
   * @param sourceChars the secret chars (will be securely zeroed after conversion)
   * @param allocator the FFM allocator to use
   * @return a new {@code FfmProtectedMemorySecret}
   */
  public static FfmProtectedMemorySecret fromCharArray(
      final char[] sourceChars, final FfmAllocator allocator) {
    Objects.requireNonNull(sourceChars, "sourceChars");
    byte[] sourceBytes = utf8CharArrayToByteArray(sourceChars);
    try {
      return new FfmProtectedMemorySecret(sourceBytes, allocator);
    }
    finally {
      secureZeroMemory(sourceBytes);
    }
  }

  private static void secureZeroMemory(final byte[] bytes) {
    Arrays.fill(bytes, (byte) 0);
  }

  private static void secureZeroMemory(final char[] chars) {
    Arrays.fill(chars, (char) 0);
  }

  private static byte[] utf8CharArrayToByteArray(final char[] sourceChars) {
    ByteBuffer bb = UTF8_CHARSET.encode(CharBuffer.wrap(sourceChars));
    byte[] bytes = new byte[bb.remaining()];
    bb.get(bytes, 0, bytes.length);
    return bytes;
  }

  private static char[] byteArrayToUtf8CharArray(final byte[] sourceBytes) {
    CharBuffer cb = UTF8_CHARSET.decode(ByteBuffer.wrap(sourceBytes));
    char[] chars = new char[cb.remaining()];
    cb.get(chars, 0, chars.length);
    return chars;
  }

  @Override
  public <T> T withSecretBytes(final Function<byte[], T> functionWithSecret) {
    Objects.requireNonNull(functionWithSecret, "functionWithSecret");
    MemorySegment currentSegment = segment;
    if (currentSegment == null || currentSegment.equals(MemorySegment.NULL)) {
      throw new IllegalStateException("Attempt to access disposed secret");
    }

    byte[] bytes = new byte[length];
    try {
      setReadAccessIfNeeded();
      try {
        if (Debug.ON) {
          LOG.debug("FFM reading: {} bytes from address {}", length, currentSegment.address());
        }
        MemorySegment.copy(currentSegment, ValueLayout.JAVA_BYTE, 0, bytes, 0, length);
      }
      finally {
        setNoAccessIfNeeded();
      }

      return functionWithSecret.apply(bytes);
    }
    finally {
      secureZeroMemory(bytes);
    }
  }

  private void setReadAccessIfNeeded() {
    accessLock.lock();
    try {
      MemorySegment current = segment;
      if (current == null) {
        throw new IllegalStateException("Attempt to access disposed secret");
      }
      // Only flip to read on the first concurrent reader.
      if (accessCounter == 0) {
        allocator.setReadAccess(current, length);
      }
      accessCounter++;
    }
    finally {
      accessLock.unlock();
    }
  }

  private void setNoAccessIfNeeded() {
    accessLock.lock();
    try {
      if (accessCounter <= 0) {
        // Should be impossible — paired with setReadAccessIfNeeded under lock.
        throw new IllegalStateException(
            "accessCounter underflow; setNoAccessIfNeeded called without matching setReadAccess");
      }
      accessCounter--;
      MemorySegment current = segment;
      // Only flip back to no-access on the last concurrent reader, and only if not closed
      // mid-flight (close() handles its own access-flag transitions on the freed path).
      if (accessCounter == 0) {
        if (current != null) {
          allocator.setNoAccess(current, length);
        }
        noReaders.signalAll();
      }
    }
    finally {
      accessLock.unlock();
    }
  }

  @Override
  public <T> T withSecretUtf8Chars(final Function<char[], T> functionWithSecret) {
    Objects.requireNonNull(functionWithSecret, "functionWithSecret");
    return withSecretBytes(bytes -> {
      char[] chars = byteArrayToUtf8CharArray(bytes);
      try {
        return functionWithSecret.apply(chars);
      }
      finally {
        secureZeroMemory(chars);
      }
    });
  }

  @Override
  public Secret copySecret() {
    return withSecretBytes((byte[] bytes) -> new FfmProtectedMemorySecret(bytes, allocator));
  }

  /**
   * Closes this secret, securely zeroing and freeing its protected memory.
   *
   * <p>This call is idempotent: a second {@code close()} is a no-op.
   *
   * <p>If concurrent readers are in flight, this method waits (uninterruptibly) for all of
   * them to drain before freeing the underlying segment. This guarantees that
   * {@link #withSecretBytes} / {@link #withSecretUtf8Chars} can never observe a use-after-free.
   */
  @Override
  public void close() {
    accessLock.lock();
    try {
      MemorySegment currentSegment = segment;
      if (currentSegment == null || currentSegment.equals(MemorySegment.NULL)) {
        return; // idempotent
      }

      // Wait for any in-flight readers to finish so we don't free memory under their feet.
      while (accessCounter > 0) {
        noReaders.awaitUninterruptibly();
      }

      // Re-read after waiting in case a concurrent close() raced us to the free.
      currentSegment = segment;
      if (currentSegment == null || currentSegment.equals(MemorySegment.NULL)) {
        return;
      }

      if (Debug.ON) {
        LOG.debug("FFM closing: address {}", currentSegment.address());
      }

      // free() requires writable memory so it can zero before unmapping.
      allocator.setReadWriteAccess(currentSegment, length);
      allocator.zeroMemory(currentSegment, length);
      allocator.free(currentSegment, length);
      segment = null;
    }
    finally {
      accessLock.unlock();
    }
  }
}

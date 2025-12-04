package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.Debug;
import com.godaddy.asherah.securememory.Secret;

/**
 * FFM-based implementation of Secret using protected memory.
 * Uses Java's Foreign Function & Memory API for better performance and memory safety.
 * Requires Java 22+.
 */
public class FfmProtectedMemorySecret implements Secret, AutoCloseable {
  private static final Logger LOG = LoggerFactory.getLogger(FfmProtectedMemorySecret.class);
  private static final Charset UTF8_CHARSET = StandardCharsets.UTF_8;

  private final FfmAllocator allocator;
  private volatile MemorySegment segment;
  private final int length;

  // IMPORTANT: accessCounter is not volatile nor atomic since we use accessLock for all read and write
  // access. If that changes, update the counter accordingly!
  private long accessCounter = 0;
  private final Lock accessLock = new ReentrantLock(true); // use fairness in case of bursts to avoid starvation

  /**
   * Creates a new FFM-based protected memory secret from byte array.
   *
   * @param sourceBytes the secret bytes (will be securely zeroed after copy)
   * @param allocator the FFM allocator to use
   */
  FfmProtectedMemorySecret(final byte[] sourceBytes, final FfmAllocator allocator) {
    this.allocator = allocator;
    this.length = sourceBytes.length;
    this.segment = allocator.alloc(length);

    if (segment == null || segment.equals(MemorySegment.NULL)) {
      throw new FfmAllocationFailed("Protected memory allocation failed");
    }
    else if (Debug.ON) {
      LOG.debug("FFM allocated: {} bytes at address {}", length, segment.address());
    }

    // Copy source bytes to protected memory
    MemorySegment.copy(sourceBytes, 0, segment, ValueLayout.JAVA_BYTE, 0, length);

    // Set memory to no-access
    allocator.setNoAccess(segment, length);

    // Only if we're going to be successful do we want to clear the client's source buffer
    secureZeroMemory(sourceBytes);
  }

  /**
   * Creates a new FFM-based protected memory secret from char array.
   *
   * @param sourceChars the secret chars (will be securely zeroed after conversion)
   * @param allocator the FFM allocator to use
   * @return a new FfmProtectedMemorySecret
   */
  public static FfmProtectedMemorySecret fromCharArray(final char[] sourceChars, final FfmAllocator allocator) {
    byte[] sourceBytes = utf8CharArrayToByteArray(sourceChars);
    try {
      return new FfmProtectedMemorySecret(sourceBytes, allocator);
    }
    finally {
      secureZeroMemory(sourceBytes);
    }
  }

  private static void secureZeroMemory(final byte[] bytes) {
    // Make sure this can't be optimized away
    Arrays.fill(bytes, (byte) 0);
  }

  private static void secureZeroMemory(final char[] chars) {
    // Make sure this can't be optimized away
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

        // Copy from protected memory to byte array
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
      // Only set read access if we're the first one trying to access this potentially-shared Secret
      if (accessCounter == 0) {
        allocator.setReadAccess(segment, length);
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
      accessCounter--;
      // Only set no access if we're the last one trying to access this potentially-shared Secret
      if (accessCounter == 0) {
        allocator.setNoAccess(segment, length);
      }
    }
    finally {
      accessLock.unlock();
    }
  }

  @Override
  public <T> T withSecretUtf8Chars(final Function<char[], T> functionWithSecret) {
    if (segment == null || segment.equals(MemorySegment.NULL)) {
      throw new IllegalStateException("Attempt to access disposed secret");
    }

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

  @Override
  public void close() {
    MemorySegment currentSegment = segment;
    if (Debug.ON) {
      String address = "null";
      if (currentSegment != null) {
        address = String.valueOf(currentSegment.address());
      }
      LOG.debug("FFM closing: address {}", address);
    }

    // Only need to do this if segment not yet closed
    if (currentSegment != null && !currentSegment.equals(MemorySegment.NULL)) {
      allocator.setReadWriteAccess(currentSegment, length);
      allocator.zeroMemory(currentSegment, length);
      allocator.free(currentSegment, length);
      segment = null;
    }
  }
}


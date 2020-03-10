package com.godaddy.asherah.securememory.protectedmemoryimpl;

import com.godaddy.asherah.securememory.Debug;
import com.godaddy.asherah.securememory.Secret;
import com.sun.jna.Pointer;

import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.Charset;
import java.util.Arrays;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class ProtectedMemorySecret implements Secret, AutoCloseable {
  private static final Logger LOG = LoggerFactory.getLogger(ProtectedMemorySecret.class);

  private static final long NULL = 0;
  private static final Charset UTF8_CHARSET = Charset.forName("UTF-8");

  private final ProtectedMemoryAllocator allocator;
  private final Pointer pointer;
  private final int length;

  // IMPORTANT: accessCounter is not volatile nor atomic since we use accessLock for all read and write
  // access. If that changes, update the counter accordingly!
  private long accessCounter = 0;
  private final Lock accessLock = new ReentrantLock(true); // use fairness in case of bursts to avoid starvation

  ProtectedMemorySecret(final byte[] sourceBytes, final ProtectedMemoryAllocator allocator) {
    this.allocator = allocator;
    this.length = sourceBytes.length;
    this.pointer = allocator.alloc(length);

    if (Pointer.nativeValue(pointer) == NULL) {
      // Don't think this will ever execute. Would expect it throw exception in the alloc
      throw new ProtectedMemoryAllocationFailed("Protected memory allocation failed");
    }
    else if (Debug.ON) {
      LOG.debug("allocated: {}", pointer);
    }

    pointer.write(0, sourceBytes, 0, length);
    allocator.setNoAccess(pointer, length);

    //Only if we're going to be successful do we want to clear the client's source buffer
    secureZeroMemory(sourceBytes);
  }

  public static ProtectedMemorySecret fromCharArray(final char[] sourceChars,
      final ProtectedMemoryAllocator allocator) {

    byte[] sourceBytes = utf8CharArrayToByteArray(sourceChars);
    try {
      return new ProtectedMemorySecret(sourceBytes, allocator);
    }
    finally {
      secureZeroMemory(sourceBytes);
    }
  }

  private static void secureZeroMemory(final byte[] bytes) {
    //Make sure this can't be optimized away
    Arrays.fill(bytes, (byte) 0);
  }

  private static void secureZeroMemory(final char[] chars) {
    //Make sure this can't be optimized away
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

  public <T> T withSecretBytes(final Function<byte[], T> functionWithSecret) {
    if (Pointer.nativeValue(pointer) == NULL) {
      throw new IllegalStateException("Attempt to access disposed secret");
    }

    byte[] bytes = new byte[length];
    try {
      setReadAccessIfNeeded();
      try {
        if (Debug.ON) {
          LOG.debug("reading: {}", pointer);
        }

        pointer.read(0, bytes, 0, bytes.length);
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
        allocator.setReadAccess(pointer, length);
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
        allocator.setNoAccess(pointer, length);
      }
    }
    finally {
      accessLock.unlock();
    }
  }

  public <T> T withSecretUtf8Chars(final Function<char[], T> functionWithSecret) {
    if (Pointer.nativeValue(pointer) == NULL) {
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
    return withSecretBytes((byte[] bytes) -> new ProtectedMemorySecret(bytes, allocator));
  }

  @Override
  protected void finalize() throws Throwable {
    // TODO this may need to all go away since finalize now deprecated
    close();
    super.finalize();
  }

  @Override
  public void close() {
    if (Debug.ON) {
      LOG.debug("closing: {}", pointer);
    }

    // only need to do this if peer not yet closed.
    // TODO peer/Pointer in general may need locking around any operations
    if (Pointer.nativeValue(pointer) != NULL) {
      allocator.setReadWriteAccess(pointer, length);
      pointer.clear(length);
      allocator.free(pointer, length);
      Pointer.nativeValue(pointer, NULL);
    }
  }
}

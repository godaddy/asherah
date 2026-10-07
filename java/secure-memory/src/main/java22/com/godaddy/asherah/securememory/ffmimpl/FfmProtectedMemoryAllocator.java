package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.MemoryLayout;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.Debug;

/**
 * Abstract base class for FFM-based protected memory allocation.
 *
 * <p>Uses Java's Foreign Function &amp; Memory API (FFM, stable since Java 22) for native libc
 * calls (mmap, mprotect, mlock, etc.). Native symbol resolution is performed lazily via the
 * Initialization-on-Demand Holder pattern so this class is safe to reference at GraalVM
 * native-image build time without forcing any FFM-related static initializer to run too early.
 *
 * <p>Requires Java 22+ at runtime.
 */
public abstract class FfmProtectedMemoryAllocator implements FfmAllocator {
  private static final Logger LOG = LoggerFactory.getLogger(FfmProtectedMemoryAllocator.class);

  /** {@code mmap} returns {@code (void*) -1} on failure (i.e. {@code MAP_FAILED}). */
  private static final long MAP_FAILED = -1L;

  /** Byte offset of {@code rlim_max} in {@code struct rlimit} (after 8-byte {@code rlim_cur}). */
  private static final long RLIM_MAX_OFFSET = 8L;

  /** Sentinel meaning "no resource cap" returned by getrlimit. */
  private static final long RLIM_INFINITY = -1L;

  /** {@code struct rlimit { rlim_cur, rlim_max }} laid out as two longs. */
  protected static final MemoryLayout RLIMIT_LAYOUT = MemoryLayout.structLayout(
      ValueLayout.JAVA_LONG.withName("rlim_cur"),
      ValueLayout.JAVA_LONG.withName("rlim_max")
  );

  /**
   * Lazy-initialized libc handles common to all platforms.
   *
   * <p>The JLS guarantees that this nested class will not be loaded until first reference,
   * and that its static initializer runs exactly once under thread-safe conditions
   * (Initialization-on-Demand Holder). This is the recommended replacement for double-checked
   * locking and is the build-time-init-safe replacement for putting these handles directly in
   * the enclosing class's {@code static} block.
   */
  private static final class Handles {
    static final MethodHandle MMAP = NativeLibc.downcall("mmap",
        FunctionDescriptor.of(
            ValueLayout.ADDRESS,    // return: void*
            ValueLayout.ADDRESS,    // addr
            ValueLayout.JAVA_LONG,  // length (size_t)
            ValueLayout.JAVA_INT,   // prot
            ValueLayout.JAVA_INT,   // flags
            ValueLayout.JAVA_INT,   // fd
            ValueLayout.JAVA_LONG)); // offset (off_t)

    static final MethodHandle MUNMAP = NativeLibc.downcall("munmap",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG));

    static final MethodHandle MPROTECT = NativeLibc.downcall("mprotect",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG,
            ValueLayout.JAVA_INT));

    static final MethodHandle MLOCK = NativeLibc.downcall("mlock",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG));

    static final MethodHandle MUNLOCK = NativeLibc.downcall("munlock",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG));

    static final MethodHandle GETRLIMIT = NativeLibc.downcall("getrlimit",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS));

    static final MethodHandle SETRLIMIT = NativeLibc.downcall("setrlimit",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS));

    private Handles() {
    }
  }

  private volatile boolean globallyDisabledCoreDumps = false;

  protected abstract int getProtRead();

  protected abstract int getProtReadWrite();

  protected abstract int getProtNoAccess();

  protected abstract int getPrivateAnonymousFlags();

  protected abstract int getMemLockLimit();

  protected abstract int getResourceCore();

  /**
   * Platform-specific implementation for marking memory as no-dump.
   *
   * @param segment the memory segment
   * @param length the length of memory
   */
  protected abstract void setNoDump(MemorySegment segment, long length);

  protected boolean areCoreDumpsGloballyDisabled() {
    return globallyDisabledCoreDumps;
  }

  protected void disableCoreDumpGlobally() {
    try (Arena arena = Arena.ofConfined()) {
      MemorySegment rlimit = arena.allocate(RLIMIT_LAYOUT);
      rlimit.set(ValueLayout.JAVA_LONG, 0, 0L);
      rlimit.set(ValueLayout.JAVA_LONG, RLIM_MAX_OFFSET, 0L);

      int result = (int) Handles.SETRLIMIT.invokeExact(getResourceCore(), rlimit);
      checkZero(result, "setrlimit(RLIMIT_CORE)");
      globallyDisabledCoreDumps = true;
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("setrlimit(RLIMIT_CORE)", t);
    }
  }

  @Override
  public void setNoAccess(final MemorySegment segment, final long length) {
    try {
      int result = (int) Handles.MPROTECT.invokeExact(segment, length, getProtNoAccess());
      checkZero(result, "mprotect(PROT_NONE)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_NONE)", t);
    }
  }

  @Override
  public void setReadAccess(final MemorySegment segment, final long length) {
    try {
      int result = (int) Handles.MPROTECT.invokeExact(segment, length, getProtRead());
      checkZero(result, "mprotect(PROT_READ)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_READ)", t);
    }
  }

  @Override
  public void setReadWriteAccess(final MemorySegment segment, final long length) {
    try {
      int result = (int) Handles.MPROTECT.invokeExact(segment, length, getProtReadWrite());
      checkZero(result, "mprotect(PROT_READ|PROT_WRITE)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_READ|PROT_WRITE)", t);
    }
  }

  @Override
  public MemorySegment alloc(final long length) {
    if (length <= 0) {
      throw new IllegalArgumentException("length must be positive, got: " + length);
    }
    if (Debug.ON) {
      LOG.debug("FFM attempting to alloc length {}", length);
    }

    enforceMemLockLimit(length);

    MemorySegment protectedMemory = mmapAnonymous(length);

    try {
      int mlockResult = (int) Handles.MLOCK.invokeExact(protectedMemory, length);
      checkZero(mlockResult, "mlock");
    }
    catch (Throwable t) {
      munmapBestEffort(protectedMemory, length);
      if (t instanceof FfmOperationFailed ffmOperationFailed) {
        throw ffmOperationFailed;
      }
      throw new FfmOperationFailed("mlock", t);
    }

    try {
      setNoDump(protectedMemory, length);
    }
    catch (RuntimeException e) {
      munlockBestEffort(protectedMemory, length);
      munmapBestEffort(protectedMemory, length);
      throw e;
    }

    return protectedMemory;
  }

  private void enforceMemLockLimit(final long length) {
    try (Arena arena = Arena.ofConfined()) {
      MemorySegment rlimit = arena.allocate(RLIMIT_LAYOUT);
      int rlimitResult = (int) Handles.GETRLIMIT.invokeExact(getMemLockLimit(), rlimit);
      checkZero(rlimitResult, "getrlimit");

      long rlimMax = rlimit.get(ValueLayout.JAVA_LONG, RLIM_MAX_OFFSET);
      if (rlimMax != RLIM_INFINITY && rlimMax < length) {
        throw new FfmMemoryLimitException(String.format(
            "Requested MemLock length %d exceeds resource limit max of: %d", length, rlimMax));
      }
    }
    catch (FfmMemoryLimitException | FfmOperationFailed e) {
      throw e;
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("getrlimit", t);
    }
  }

  private MemorySegment mmapAnonymous(final long length) {
    try {
      MemorySegment segment = (MemorySegment) Handles.MMAP.invokeExact(
          MemorySegment.NULL,
          length,
          getProtReadWrite(),
          getPrivateAnonymousFlags(),
          -1,
          0L);

      if (segment.address() == MAP_FAILED || segment.equals(MemorySegment.NULL)) {
        throw new FfmAllocationFailed("mmap returned MAP_FAILED or NULL");
      }
      return segment.reinterpret(length);
    }
    catch (FfmAllocationFailed e) {
      throw e;
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mmap", t);
    }
  }

  private static void munlockBestEffort(final MemorySegment segment, final long length) {
    try {
      Handles.MUNLOCK.invokeExact(segment, length);
    }
    catch (Throwable t) {
      LOG.warn("munlock failed during cleanup", t);
    }
  }

  private static void munmapBestEffort(final MemorySegment segment, final long length) {
    try {
      Handles.MUNMAP.invokeExact(segment, length);
    }
    catch (Throwable t) {
      LOG.warn("munmap failed during cleanup", t);
    }
  }

  @Override
  public void free(final MemorySegment segment, final long length) {
    try {
      // Wipe the protected memory (assumes memory was made writeable by caller).
      zeroMemory(segment, length);
    }
    finally {
      try {
        int munlockResult = (int) Handles.MUNLOCK.invokeExact(segment, length);
        checkZero(munlockResult, "munlock");
      }
      catch (Throwable t) {
        LOG.warn("munlock failed during free", t);
      }
      finally {
        try {
          int munmapResult = (int) Handles.MUNMAP.invokeExact(segment, length);
          checkZero(munmapResult, "munmap");
        }
        catch (Throwable t) {
          throw new FfmOperationFailed("munmap", t);
        }
      }
    }
  }

  protected void checkZero(final int result, final String methodName) {
    if (result != 0) {
      throw new FfmOperationFailed(methodName, result);
    }
  }
}

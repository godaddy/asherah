package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemoryLayout;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.Debug;

/**
 * Abstract base class for FFM-based protected memory allocation.
 * Uses Java's Foreign Function & Memory API (FFM) for native calls.
 * Requires Java 22+.
 */
public abstract class FfmProtectedMemoryAllocator implements FfmAllocator {
  private static final Logger LOG = LoggerFactory.getLogger(FfmProtectedMemoryAllocator.class);

  private static final long MAP_FAILED = -1L;

  /** Byte offset of rlim_max field in rlimit struct (after rlim_cur which is 8 bytes). */
  private static final long RLIM_MAX_OFFSET = 8L;

  // Native function handles
  private static final MethodHandle MMAP;
  private static final MethodHandle MUNMAP;
  private static final MethodHandle MPROTECT;
  private static final MethodHandle MLOCK;
  private static final MethodHandle MUNLOCK;
  private static final MethodHandle GETRLIMIT;
  private static final MethodHandle SETRLIMIT;

  // rlimit structure layout: { rlim_cur, rlim_max } as two longs
  protected static final MemoryLayout RLIMIT_LAYOUT = MemoryLayout.structLayout(
      ValueLayout.JAVA_LONG.withName("rlim_cur"),
      ValueLayout.JAVA_LONG.withName("rlim_max")
  );

  static {
    Linker linker = Linker.nativeLinker();
    SymbolLookup libc = linker.defaultLookup();

    // void* mmap(void* addr, size_t length, int prot, int flags, int fd, off_t offset)
    MMAP = linker.downcallHandle(
        libc.find("mmap").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.ADDRESS,      // return: void*
            ValueLayout.ADDRESS,      // addr
            ValueLayout.JAVA_LONG,    // length (size_t)
            ValueLayout.JAVA_INT,     // prot
            ValueLayout.JAVA_INT,     // flags
            ValueLayout.JAVA_INT,     // fd
            ValueLayout.JAVA_LONG     // offset (off_t)
        )
    );

    // int munmap(void* addr, size_t length)
    MUNMAP = linker.downcallHandle(
        libc.find("munmap").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG
        )
    );

    // int mprotect(void* addr, size_t len, int prot)
    MPROTECT = linker.downcallHandle(
        libc.find("mprotect").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG,
            ValueLayout.JAVA_INT
        )
    );

    // int mlock(const void* addr, size_t len)
    MLOCK = linker.downcallHandle(
        libc.find("mlock").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG
        )
    );

    // int munlock(const void* addr, size_t len)
    MUNLOCK = linker.downcallHandle(
        libc.find("munlock").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG
        )
    );

    // int getrlimit(int resource, struct rlimit* rlim)
    GETRLIMIT = linker.downcallHandle(
        libc.find("getrlimit").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS
        )
    );

    // int setrlimit(int resource, const struct rlimit* rlim)
    SETRLIMIT = linker.downcallHandle(
        libc.find("setrlimit").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS
        )
    );
  }

  // Platform-specific constants (to be implemented by subclasses)
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

  private volatile boolean globallyDisabledCoreDumps = false;

  protected boolean areCoreDumpsGloballyDisabled() {
    return globallyDisabledCoreDumps;
  }

  protected void disableCoreDumpGlobally() {
    try (Arena arena = Arena.ofConfined()) {
      MemorySegment rlimit = arena.allocate(RLIMIT_LAYOUT);
      rlimit.set(ValueLayout.JAVA_LONG, 0, 0L); // rlim_cur = 0
      rlimit.set(ValueLayout.JAVA_LONG, RLIM_MAX_OFFSET, 0L); // rlim_max = 0

      int result = (int) SETRLIMIT.invokeExact(getResourceCore(), rlimit);
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
      int result = (int) MPROTECT.invokeExact(segment, length, getProtNoAccess());
      checkZero(result, "mprotect(PROT_NONE)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_NONE)", t);
    }
  }

  @Override
  public void setReadAccess(final MemorySegment segment, final long length) {
    try {
      int result = (int) MPROTECT.invokeExact(segment, length, getProtRead());
      checkZero(result, "mprotect(PROT_READ)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_READ)", t);
    }
  }

  @Override
  public void setReadWriteAccess(final MemorySegment segment, final long length) {
    try {
      int result = (int) MPROTECT.invokeExact(segment, length, getProtReadWrite());
      checkZero(result, "mprotect(PROT_READ|PROT_WRITE)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mprotect(PROT_READ|PROT_WRITE)", t);
    }
  }

  @Override
  public MemorySegment alloc(final long length) {
    if (Debug.ON) {
      LOG.debug("FFM attempting to alloc length {}", length);
    }

    try (Arena arena = Arena.ofConfined()) {
      // Check requested length against rlimit max memlock size
      MemorySegment rlimit = arena.allocate(RLIMIT_LAYOUT);
      int rlimitResult = (int) GETRLIMIT.invokeExact(getMemLockLimit(), rlimit);
      checkZero(rlimitResult, "getrlimit");

      long rlimMax = rlimit.get(ValueLayout.JAVA_LONG, RLIM_MAX_OFFSET);
      if (rlimMax != -1L && rlimMax < length) {
        throw new FfmMemoryLimitException(String.format(
            "Requested MemLock length exceeds resource limit max of: %d", rlimMax));
      }
    }
    catch (FfmMemoryLimitException e) {
      throw e;
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("getrlimit", t);
    }

    MemorySegment protectedMemory;
    try {
      // mmap with MAP_PRIVATE | MAP_ANONYMOUS
      protectedMemory = (MemorySegment) MMAP.invokeExact(
          MemorySegment.NULL,
          length,
          getProtReadWrite(),
          getPrivateAnonymousFlags(),
          -1,
          0L
      );

      // Check for MAP_FAILED
      if (protectedMemory.address() == MAP_FAILED || protectedMemory.equals(MemorySegment.NULL)) {
        throw new FfmAllocationFailed("mmap returned MAP_FAILED or NULL");
      }

      // Reinterpret with proper size for subsequent operations
      protectedMemory = protectedMemory.reinterpret(length);

    }
    catch (FfmAllocationFailed e) {
      throw e;
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("mmap", t);
    }

    try {
      // Lock memory to prevent swapping
      int mlockResult = (int) MLOCK.invokeExact(protectedMemory, length);
      checkZero(mlockResult, "mlock");

      try {
        // Mark as no-dump (platform-specific)
        setNoDump(protectedMemory, length);
      }
      catch (Exception e) {
        // Cleanup on failure
        try {
          MUNLOCK.invokeExact(protectedMemory, length);
        }
        catch (Throwable ignored) {
          // Best effort cleanup
        }
        throw e;
      }
    }
    catch (FfmOperationFailed e) {
      // Cleanup mmap on failure
      try {
        MUNMAP.invokeExact(protectedMemory, length);
      }
      catch (Throwable ignored) {
        // Best effort cleanup
      }
      throw e;
    }
    catch (Throwable t) {
      // Cleanup mmap on failure
      try {
        MUNMAP.invokeExact(protectedMemory, length);
      }
      catch (Throwable ignored) {
        // Best effort cleanup
      }
      throw new FfmOperationFailed("mlock", t);
    }

    return protectedMemory;
  }

  @Override
  public void free(final MemorySegment segment, final long length) {
    try {
      // Wipe the protected memory (assumes memory was made writeable)
      zeroMemory(segment, length);
    }
    finally {
      try {
        // Unlock the protected memory
        int munlockResult = (int) MUNLOCK.invokeExact(segment, length);
        checkZero(munlockResult, "munlock");
      }
      catch (Throwable t) {
        LOG.warn("munlock failed during free", t);
      }
      finally {
        try {
          // Free (unmap) the protected memory
          int munmapResult = (int) MUNMAP.invokeExact(segment, length);
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


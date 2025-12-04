package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

/**
 * Linux-specific FFM protected memory implementation.
 *
 * <p>Supports:
 * <ul>
 *   <li>mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous</li>
 *   <li>mlock() - Locked (no swap)</li>
 *   <li>madvise(MADV_DONTDUMP) - Selectively disable core dump for memory ranges</li>
 * </ul>
 */
public class LinuxFfmProtectedMemoryAllocator extends FfmProtectedMemoryAllocator {

  // Linux-specific constants
  private static final int PROT_NONE = 0x0;
  private static final int PROT_READ = 0x01;
  private static final int PROT_WRITE = 0x02;

  private static final int MAP_PRIVATE = 0x02;
  private static final int MAP_ANONYMOUS = 0x20;

  private static final int RLIMIT_CORE = 4;
  private static final int RLIMIT_MEMLOCK = 8;

  private static final int MADV_DONTDUMP = 16;

  // Native function handles for Linux-specific calls
  private static final MethodHandle MADVISE;
  private static final MethodHandle BZERO;

  static {
    Linker linker = Linker.nativeLinker();
    SymbolLookup libc = linker.defaultLookup();

    // int madvise(void* addr, size_t length, int advice)
    MADVISE = linker.downcallHandle(
        libc.find("madvise").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG,
            ValueLayout.JAVA_INT
        )
    );

    // void bzero(void* s, size_t n)
    BZERO = linker.downcallHandle(
        libc.find("bzero").orElseThrow(),
        FunctionDescriptor.ofVoid(
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG
        )
    );
  }

  @Override
  protected int getProtRead() {
    return PROT_READ;
  }

  @Override
  protected int getProtReadWrite() {
    return PROT_READ | PROT_WRITE;
  }

  @Override
  protected int getProtNoAccess() {
    return PROT_NONE;
  }

  @Override
  protected int getPrivateAnonymousFlags() {
    return MAP_PRIVATE | MAP_ANONYMOUS;
  }

  @Override
  protected int getMemLockLimit() {
    return RLIMIT_MEMLOCK;
  }

  @Override
  protected int getResourceCore() {
    return RLIMIT_CORE;
  }

  @Override
  protected void setNoDump(final MemorySegment segment, final long length) {
    try {
      int result = (int) MADVISE.invokeExact(segment, length, MADV_DONTDUMP);
      checkZero(result, "madvise(MADV_DONTDUMP)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("madvise(MADV_DONTDUMP)", t);
    }
  }

  @Override
  public void zeroMemory(final MemorySegment segment, final long length) {
    try {
      // Glibc bzero doesn't seem to be vulnerable to being optimized away
      BZERO.invokeExact(segment, length);
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("bzero", t);
    }
  }
}


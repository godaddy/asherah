package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

/**
 * Linux-specific FFM protected memory implementation.
 *
 * <p>Supports:
 * <ul>
 *   <li>{@code mmap(MAP_PRIVATE | MAP_ANON)} - private anonymous mapping</li>
 *   <li>{@code mlock()} - locked, no swap</li>
 *   <li>{@code madvise(MADV_DONTDUMP)} - selectively exclude from core dumps</li>
 *   <li>{@code bzero()} - secure zero (glibc bzero is not subject to dead-store elimination)</li>
 * </ul>
 *
 * <p>Native symbol resolution is performed lazily through a private holder class so this type
 * is safe to load at GraalVM native-image build time.
 */
public class LinuxFfmProtectedMemoryAllocator extends FfmProtectedMemoryAllocator {

  // Linux-specific syscall constants (from <sys/mman.h>, <sys/resource.h>).
  private static final int PROT_NONE = 0x0;
  private static final int PROT_READ = 0x01;
  private static final int PROT_WRITE = 0x02;

  private static final int MAP_PRIVATE = 0x02;
  private static final int MAP_ANONYMOUS = 0x20;

  private static final int RLIMIT_CORE = 4;
  private static final int RLIMIT_MEMLOCK = 8;

  private static final int MADV_DONTDUMP = 16;

  /** Lazy-init holder. JLS class-init guarantees thread safety without volatile/synchronized. */
  private static final class Handles {
    static final MethodHandle MADVISE = NativeLibc.downcall("madvise",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG,
            ValueLayout.JAVA_INT));

    static final MethodHandle BZERO = NativeLibc.downcall("bzero",
        FunctionDescriptor.ofVoid(
            ValueLayout.ADDRESS,
            ValueLayout.JAVA_LONG));

    private Handles() {
    }
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
      int result = (int) Handles.MADVISE.invokeExact(segment, length, MADV_DONTDUMP);
      checkZero(result, "madvise(MADV_DONTDUMP)");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("madvise(MADV_DONTDUMP)", t);
    }
  }

  @Override
  public void zeroMemory(final MemorySegment segment, final long length) {
    try {
      // glibc bzero is not subject to dead-store elimination, unlike memset(0).
      Handles.BZERO.invokeExact(segment, length);
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("bzero", t);
    }
  }
}

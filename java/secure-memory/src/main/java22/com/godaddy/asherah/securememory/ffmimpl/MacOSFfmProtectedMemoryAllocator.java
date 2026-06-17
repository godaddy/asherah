package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

/**
 * macOS-specific FFM protected memory implementation.
 *
 * <p>Supports:
 * <ul>
 *   <li>{@code mmap(MAP_PRIVATE | MAP_ANON)} - private anonymous mapping</li>
 *   <li>{@code mlock()} - locked, no swap</li>
 *   <li>{@code setrlimit(RLIMIT_CORE, 0)} - globally disable core dumps for the process</li>
 *   <li>{@code memset_s()} - C11 secure zero, guaranteed not to be optimized away</li>
 * </ul>
 *
 * <p>macOS does not provide {@code madvise(MADV_DONTDUMP)}, so core dumps are disabled
 * <em>process-wide</em> on construction and re-checked when individual allocations request
 * "no dump". This is the same trade-off the JNA implementation makes.
 *
 * <p>Native symbol resolution is performed lazily through a private holder class so this type
 * is safe to load at GraalVM native-image build time.
 */
public class MacOSFfmProtectedMemoryAllocator extends FfmProtectedMemoryAllocator {

  // macOS-specific syscall constants (from <sys/mman.h>, <sys/resource.h>).
  private static final int PROT_NONE = 0x00;
  private static final int PROT_READ = 0x01;
  private static final int PROT_WRITE = 0x02;

  private static final int MAP_PRIVATE = 0x0002;
  private static final int MAP_ANONYMOUS = 0x1000;

  private static final int RLIMIT_CORE = 4;
  private static final int RLIMIT_MEMLOCK = 6;

  /** Lazy-init holder. JLS class-init guarantees thread safety without volatile/synchronized. */
  private static final class Handles {
    /** {@code errno_t memset_s(void* dest, rsize_t destsz, int c, rsize_t n)}. */
    static final MethodHandle MEMSET_S = NativeLibc.downcall("memset_s",
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,    // return: errno_t
            ValueLayout.ADDRESS,     // dest
            ValueLayout.JAVA_LONG,   // destsz (rsize_t)
            ValueLayout.JAVA_INT,    // c
            ValueLayout.JAVA_LONG)); // n (rsize_t)

    private Handles() {
    }
  }

  /**
   * Creates a new macOS FFM allocator. Disables core dumps globally on construction because
   * macOS lacks {@code MADV_DONTDUMP}.
   */
  public MacOSFfmProtectedMemoryAllocator() {
    disableCoreDumpGlobally();
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
    if (areCoreDumpsGloballyDisabled()) {
      return;
    }
    disableCoreDumpGlobally();
    if (!areCoreDumpsGloballyDisabled()) {
      throw new FfmOperationFailed("Failed to disable core dumps");
    }
  }

  @Override
  public void zeroMemory(final MemorySegment segment, final long length) {
    try {
      // memset_s is C11 Annex K and is guaranteed by spec not to be optimized away.
      int result = (int) Handles.MEMSET_S.invokeExact(segment, length, 0, length);
      checkZero(result, "memset_s");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("memset_s", t);
    }
  }
}

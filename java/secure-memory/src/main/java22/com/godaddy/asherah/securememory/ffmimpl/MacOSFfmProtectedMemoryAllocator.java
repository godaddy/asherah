package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

/**
 * macOS-specific FFM protected memory implementation.
 *
 * <p>Supports:
 * <ul>
 *   <li>mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous</li>
 *   <li>mlock() - Locked (no swap)</li>
 *   <li>setrlimit(RLIMIT_CORE, 0) - Globally disable core dumps</li>
 * </ul>
 *
 * <p>Note: macOS doesn't support madvise(MADV_DONTDUMP), so core dumps
 * are disabled globally instead.
 */
public class MacOSFfmProtectedMemoryAllocator extends FfmProtectedMemoryAllocator {

  // macOS-specific constants
  private static final int PROT_NONE = 0x00;
  private static final int PROT_READ = 0x01;
  private static final int PROT_WRITE = 0x02;

  private static final int MAP_PRIVATE = 0x0002;
  private static final int MAP_ANONYMOUS = 0x1000;

  private static final int RLIMIT_CORE = 4;
  private static final int RLIMIT_MEMLOCK = 6;

  // Native function handle for macOS-specific secure memory zeroing
  private static final MethodHandle MEMSET_S;

  static {
    Linker linker = Linker.nativeLinker();
    SymbolLookup libc = linker.defaultLookup();

    // errno_t memset_s(void* dest, rsize_t destsz, int c, rsize_t n)
    MEMSET_S = linker.downcallHandle(
        libc.find("memset_s").orElseThrow(),
        FunctionDescriptor.of(
            ValueLayout.JAVA_INT,     // return: errno_t
            ValueLayout.ADDRESS,      // dest
            ValueLayout.JAVA_LONG,    // destsz (rsize_t)
            ValueLayout.JAVA_INT,     // c (value to set)
            ValueLayout.JAVA_LONG     // n (rsize_t)
        )
    );
  }

  /**
   * Creates a new macOS FFM allocator.
   * Automatically disables core dumps globally.
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
    // macOS doesn't have madvise(MADV_DONTDUMP), so we disable core dumps globally
    if (!areCoreDumpsGloballyDisabled()) {
      disableCoreDumpGlobally();
      if (!areCoreDumpsGloballyDisabled()) {
        throw new RuntimeException("Failed to disable core dumps");
      }
    }
  }

  @Override
  public void zeroMemory(final MemorySegment segment, final long length) {
    try {
      // macOS has memset_s which is standardized and secure (cannot be optimized away)
      int result = (int) MEMSET_S.invokeExact(segment, length, 0, length);
      checkZero(result, "memset_s");
    }
    catch (Throwable t) {
      throw new FfmOperationFailed("memset_s", t);
    }
  }
}


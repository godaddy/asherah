package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

import com.godaddy.asherah.securememory.protectedmemoryimpl.libc.LibcProtectedMemoryAllocator;
import com.sun.jna.NativeLong;
import com.sun.jna.Pointer;

/*
 * MacOS protected memory implementation supports:
 *
 * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
 * mlock() - Locked (no swap)
 * setrlimit(RLIMIT_CORE, 0) - Globally disable core dumps
 */

public class MacOSProtectedMemoryAllocator extends LibcProtectedMemoryAllocator {
  private final MacOSLibc libc;

  public MacOSProtectedMemoryAllocator(final MacOSLibc libc) {
    super(libc);
    this.libc = libc;
    disableCoreDumpGlobally();
  }

  //Platform specific zero memory

  @Override
  public void zeroMemory(final Pointer pointer, final long length) {
    //This differs on different platforms
    //MacOS has memset_s which is standardized and secure
    NativeLong nLength = new NativeLong(length);
    libc.memset_s(pointer, nLength, 0, nLength);
  }

  //Platform specific blocking memory from core dump

  @Override
  protected void setNoDump(final Pointer pointer, final long length) {
    //MacOS doesn't have madvise(MAP_DONTDUMP) so we have to disable core dumps globally
    if (!areCoreDumpsGloballyDisabled()) {
      disableCoreDumpGlobally();
      if (!areCoreDumpsGloballyDisabled()) {
        //TODO Make this a strongly typed exception
        throw new RuntimeException("Failed to disable core dumps");
      }
    }
  }

  //These flags are platform specific in their integer values

  @Override
  protected int getProtRead() {
    return MemoryMapProtection.PROT_READ;
  }

  @Override
  protected int getProtReadWrite() {
    return MemoryMapProtection.PROT_READ | MemoryMapProtection.PROT_WRITE;
  }

  @Override
  protected int getProtNoAccess() {
    return MemoryMapProtection.PROT_NONE;
  }

  @Override
  protected int getPrivateAnonymousFlags() {
    return MemoryMapFlag.MAP_PRIVATE | MemoryMapFlag.MAP_ANON;
  }

  @Override
  protected int getResourceCore() {
    return ResourceLimit.RLIMIT_CORE;
  }

  @Override
  protected int getMemLockLimit() {
    return ResourceLimit.RLIMIT_MEMLOCK;
  }
}

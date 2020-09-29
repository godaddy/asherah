package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

import com.godaddy.asherah.securememory.protectedmemoryimpl.libc.LibcProtectedMemoryAllocator;
import com.sun.jna.NativeLong;
import com.sun.jna.Pointer;

/*
 * Linux protected memory implementation supports:
 *
 * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
 * mlock() - Locked (no swap)
 * madvise(MADV_DONTDUMP) - Selectively disable core dump for memory ranges
 */

public class LinuxProtectedMemoryAllocator extends LibcProtectedMemoryAllocator {
  private final LinuxLibc libc;

  public LinuxProtectedMemoryAllocator(final LinuxLibc libc) {
    super(libc);
    this.libc = libc;
  }

  //Platform specific zero memory

  @Override
  public void zeroMemory(final Pointer pointer, final long length) {
    libc.bzero(pointer, new NativeLong(length));
  }

  //Platform specific blocking memory from core dump

  @Override
  public void setNoDump(final Pointer pointer, final long length) {
    checkZero(libc.madvise(pointer, new NativeLong(length), MemoryAdvice.MADV_DONTDUMP), "madvise(MADV_DONTDUMP)");
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

package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

@SuppressWarnings("unused")
final class MemoryLockFlag {
  private MemoryLockFlag() { }

  public static final int MCL_CURRENT = 0x0001;

  public static final int MCL_FUTURE = 0x0002;
}

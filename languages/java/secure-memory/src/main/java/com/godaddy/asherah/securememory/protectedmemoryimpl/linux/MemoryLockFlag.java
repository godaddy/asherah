package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

@SuppressWarnings("unused")
final class MemoryLockFlag {
  private MemoryLockFlag() { }

  public static final int MCL_CURRENT = 1;

  public static final int MCL_FUTURE = 2;

  public static final int MCL_ONFAULT = 4;
}

package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

@SuppressWarnings("unused")
final class MemoryAdvice {
  private MemoryAdvice() { }

  public static final int POSIX_MADV_NORMAL = 0;
  public static final int POSIX_MADV_RANDOM = 1;
  public static final int POSIC_MADV_SEQUENTIAL = 2;
  public static final int POSIX_MADV_WILLNEED = 3;
  public static final int POSIX_MADV_DONTNEED = 4;

  public static final int MADV_NORMAL = 0;
  public static final int MADV_RANDOM = 1;
  public static final int MADV_SEQUENTIAL = 2;
  public static final int MADV_WILLNEED = 3;
  public static final int MADV_DONTNEED = 4;
  public static final int MADV_FREE = 5;
  public static final int MADV_ZERO_WIRED_PAGES = 6;
  public static final int MADV_FREE_REUSABLE = 7;
  public static final int MADV_FREE_REUSE = 8;
  public static final int MADV_CAN_REUSE = 9;
  public static final int MADV_PAGEOUT = 10;
}

package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

@SuppressWarnings("unused")
final class MemoryMapFlag {
  private MemoryMapFlag() { }

  public static final int MAP_SHARED = 0x01;

  public static final int MAP_PRIVATE = 0x02;

  public static final int MAP_TYPE = 0x0f;

  public static final int MAP_FIXED = 0x10;

  public static final int MAP_FILE = 0;

  public static final int MAP_ANONYMOUS = 0x20;

  public static final int MAP_ANON = 0x20;

  public static final int MAP_32BIT = 0x40;

  public static final int MAP_HUGE_SHIFT = 26;

  public static final int MAP_HUGE_MASK = 0x3f;

  public static final int MAP_GROWSDOWN = 0x00100;

  public static final int MAP_DENYWRITE = 0x00800;

  public static final int MAP_EXECUTABLE = 0x01000;

  public static final int MAP_LOCKED = 0x02000;

  public static final int MAP_NORESERVE = 0x04000;

  public static final int MAP_POPULATE = 0x08000;

  public static final int MAP_NONBLOCK = 0x10000;

  public static final int MAP_STACK = 0x20000;

  public static final int MAP_HUGETLB = 0x40000;
}

package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

@SuppressWarnings("unused")
final class MemoryMapFlag {
  private MemoryMapFlag() { }

  public static final int MAP_SHARED = 0x0001;

  public static final int MAP_PRIVATE = 0x0002;

  public static final int MAP_COPY = 0x0002; //MAP_PRIVATE

  public static final int MAP_FIXED = 0x0010;

  public static final int MAP_RENAME = 0x0020;

  public static final int MAP_NORESERVE = 0x0040;

  public static final int MAP_RESERVED0080 = 0x0080;

  public static final int MAP_NOEXTEND = 0x0100;

  public static final int MAP_HASSEMAPHORE = 0x0200;

  public static final int MAP_NOCACHE = 0x0400;

  public static final int MAP_JIT = 0x0800;

  public static final int MAP_FILE = 0x0000;

  public static final int MAP_ANON = 0x1000;

  public static final int MAP_ANONYMOUS = 0x1000; //MAP_ANON
}

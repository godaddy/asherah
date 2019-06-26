package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

@SuppressWarnings({"CheckStyle", "unused"})
final class  MemoryAdvice {
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
  public static final int MADV_REMOVE = 9;
  public static final int MADV_DONTFORK = 10;
  public static final int MADV_DOFORK = 11;
  public static final int MADV_MERGEABLE = 12;
  public static final int MADV_UNMERGEABLE = 13;
  public static final int MADV_HUGEPAGE = 14;
  public static final int MADV_NOHUGEPAGE = 15;
  public static final int MADV_DONTDUMP = 16;
  public static final int MADV_DODUMP = 17;
  public static final int MADV_HWPOISON = 100;
}

package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

@SuppressWarnings("unused")
final class ResourceLimit {
  private ResourceLimit() { }

  public static final int RLIMIT_CPU = 0; // cpu time per process

  public static final int RLIMIT_FSIZE = 1; // file size

  public static final int RLIMIT_DATA = 2; // data segment size

  public static final int RLIMIT_STACK = 3; // stack size

  public static final int RLIMIT_CORE = 4;  // core file size

  public static final int RLIMIT_AS = 5; // address space (resident set size)

  public static final int RLIMIT_RSS = 5; // source compatibility alias

  public static final int RLIMIT_MEMLOCK = 6; // locked-in-memory address space

  public static final int RLIMIT_NPROC = 7; // number of processes

  public static final int RLIMIT_NOFILE = 8; // number of open files
}

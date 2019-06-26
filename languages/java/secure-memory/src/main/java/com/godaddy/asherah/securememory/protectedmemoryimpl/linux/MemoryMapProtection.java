package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

@SuppressWarnings("unused")
final class MemoryMapProtection {
  private MemoryMapProtection() { }

  public static final int PROT_NONE = 0x0;

  public static final int PROT_READ = 0x01;

  public static final int PROT_WRITE = 0x02;

  public static final int PROT_EXEC = 0x04;

  public static final int PROT_GROWSDOWN = 0x01000000;

  public static final int PROT_GROWSUP = 0x02000000;
}

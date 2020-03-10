package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

@SuppressWarnings("unused")
final class MemoryMapProtection {
  private MemoryMapProtection() { }

  public static final int PROT_NONE = 0x00;

  public static final int PROT_READ = 0x01;

  public static final int PROT_WRITE = 0x02;

  public static final int PORT_EXEC = 0x04;
}

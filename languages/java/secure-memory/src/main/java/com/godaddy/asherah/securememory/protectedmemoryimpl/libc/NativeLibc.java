package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

import com.sun.jna.LastErrorException;
import com.sun.jna.Native;
import com.sun.jna.NativeLong;
import com.sun.jna.Pointer;
import com.sun.jna.Structure;

public class NativeLibc {

  static {
    Native.register(NativeLibc.class, "c");
  }

  public native int madvise(Pointer addr, NativeLong length, int advice) throws LastErrorException;

  public native int setrlimit(int resource, Structure rlim) throws LastErrorException;

  public native int getrlimit(int resource, Structure rlim) throws LastErrorException;

  public native int mlock(Pointer addr, NativeLong len) throws LastErrorException;

  public native int munlock(Pointer addr, NativeLong len) throws LastErrorException;

  public native Pointer mmap(Pointer addr, NativeLong length, int prot, int flags, int fd,
                             NativeLong offset) throws LastErrorException;

  public native int munmap(Pointer addr, NativeLong length) throws LastErrorException;

  public native int mprotect(Pointer addr, NativeLong len, int prot) throws LastErrorException;

}

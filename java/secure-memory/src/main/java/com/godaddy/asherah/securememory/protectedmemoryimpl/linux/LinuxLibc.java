package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

import com.godaddy.asherah.securememory.protectedmemoryimpl.libc.NativeLibc;
import com.sun.jna.Native;
import com.sun.jna.NativeLong;
import com.sun.jna.Platform;
import com.sun.jna.Pointer;

@SuppressWarnings("ALL")
public class LinuxLibc extends NativeLibc {

  static {
    Native.register(LinuxLibc.class, Platform.C_LIBRARY_NAME);
  }

  public native void bzero(Pointer addr, NativeLong length);

}

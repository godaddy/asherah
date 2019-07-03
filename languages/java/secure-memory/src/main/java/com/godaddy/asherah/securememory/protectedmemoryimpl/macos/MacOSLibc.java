package com.godaddy.asherah.securememory.protectedmemoryimpl.macos;

import com.godaddy.asherah.securememory.protectedmemoryimpl.libc.NativeLibc;
import com.sun.jna.Native;
import com.sun.jna.NativeLong;
import com.sun.jna.Pointer;

@SuppressWarnings("all")
public class MacOSLibc extends NativeLibc {

  static {
    Native.register(MacOSLibc.class, "c");
  }

  public native int memset_s(Pointer dest, NativeLong destSize, int c, NativeLong count);
}

package com.godaddy.asherah.securememory.protectedmemoryimpl;

import com.sun.jna.Pointer;

public interface ProtectedMemoryAllocator {
  Pointer alloc(long length);

  void free(Pointer pointer, long length);

  void setReadWriteAccess(Pointer pointer, long length);

  void setReadAccess(Pointer pointer, long length);

  void setNoAccess(Pointer pointer, long length);

  void zeroMemory(Pointer pointer, long length);

}

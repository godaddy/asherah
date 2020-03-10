package com.godaddy.asherah.securememory.protectedmemoryimpl;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;
import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxProtectedMemoryAllocator;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSProtectedMemoryAllocator;
import com.sun.jna.Platform;

public class ProtectedMemorySecretFactory implements SecretFactory {
  //Detect methods should throw if they know for sure what the OS/platform is, but it isn't supported
  //Detect methods should return null if they don't know for sure what the OS/platform is

  private final ProtectedMemoryAllocator allocator;

  public ProtectedMemorySecretFactory() {
    allocator = detectViaJNA();
    if (allocator == null) {
      throw new UnsupportedOperationException("Could not detect supported platform for protected memory");
    }
  }

  private ProtectedMemoryAllocator detectViaJNA() {
    ProtectedMemoryAllocator newAllocator = null;
    if (Platform.isMac()) {
      newAllocator = new MacOSProtectedMemoryAllocator(new MacOSLibc());
    }
    else if (Platform.isLinux()) {
      newAllocator = new LinuxProtectedMemoryAllocator(new LinuxLibc());
    }
    return newAllocator;
  }

  @Override
  public Secret createSecret(final byte[] secretData) {
    return new ProtectedMemorySecret(secretData, allocator);
  }

  @Override
  public Secret createSecret(final char[] secretData) {
    return ProtectedMemorySecret.fromCharArray(secretData, allocator);
  }
}

package com.godaddy.asherah.securememory.protectedmemoryimpl.linux;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.sun.jna.Platform;
import com.sun.jna.Pointer;

class LinuxProtectedMemoryAllocatorTest {
  LinuxProtectedMemoryAllocator linuxProtectedMemoryAllocator = null;

  @BeforeEach
  void setUp() throws Exception {
    if (Platform.isLinux()) {
      // Not going to use mock for now
      linuxProtectedMemoryAllocator = new LinuxProtectedMemoryAllocator(new LinuxLibc());
    }
  }

  @Test
  void testGetResourceCore() {
    if (linuxProtectedMemoryAllocator != null) {
      // Not sure what else to really do here? Just trying to bump up code coverage
      assertEquals(4, linuxProtectedMemoryAllocator.getResourceCore());
    }
  }

  @Test
  void testZeroMemory() {
    if (linuxProtectedMemoryAllocator != null) {
      byte[] origValue = new byte[]{1, 2, 3, 4};
      Pointer pointer = linuxProtectedMemoryAllocator.alloc(origValue.length);
      try {
        pointer.write(0,  origValue, 0, origValue.length);

        byte[] retValue = pointer.getByteArray(0, origValue.length);
        assertArrayEquals(origValue, retValue);

        linuxProtectedMemoryAllocator.zeroMemory(pointer, origValue.length);
        retValue = pointer.getByteArray(0, origValue.length);
        assertArrayEquals(new byte[]{0, 0, 0, 0}, retValue);
      }
      finally {
        linuxProtectedMemoryAllocator.free(pointer, origValue.length);
      }
    }
  }

}

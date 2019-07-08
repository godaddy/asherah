package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.linux.LinuxProtectedMemoryAllocator;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSLibc;
import com.godaddy.asherah.securememory.protectedmemoryimpl.macos.MacOSProtectedMemoryAllocator;
import com.sun.jna.LastErrorException;
import com.sun.jna.NativeLong;
import com.sun.jna.Platform;
import com.sun.jna.Pointer;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class LibcProtectedMemoryAllocatorTest {
  NativeLibc libc = null;
  LibcProtectedMemoryAllocator libcProtectedMemoryAllocator = null;

  @BeforeEach
  void setUp() {
    if (Platform.isMac()) {
      libc = new MacOSLibc();
      libcProtectedMemoryAllocator = spy(new MacOSProtectedMemoryAllocator((MacOSLibc) libc));
    }
    else if (Platform.isLinux()) {
      libc = new LinuxLibc();
      libcProtectedMemoryAllocator = spy(new LinuxProtectedMemoryAllocator((LinuxLibc) libc));
    }
  }

  @Test
  void testSetNoAccess() {
  }

  @Test
  void testSetReadAccess() {
  }

  @Test
  void testSetReadWriteAccess() {
    Pointer pointer = libcProtectedMemoryAllocator.alloc(1);
    try {
      libcProtectedMemoryAllocator.setReadWriteAccess(pointer, 1);
      // Verifies we can write and read back
      pointer.setByte(0, (byte) 42);
      assertEquals(42, pointer.getByte(0));
    }
    finally {
      libcProtectedMemoryAllocator.free(pointer, 1);
    }
  }

  @Test
  void testGetErrno() {
  }

  @Test
  void testDisableCoreDumpGlobally() {
    // Mac allocator has global core dumps disabled on init
    if(Platform.isLinux()) {
      assertFalse(libcProtectedMemoryAllocator.areCoreDumpsGloballyDisabled());
      ResourceLimit rLimit = new ResourceLimit();
      libc.getrlimit(libcProtectedMemoryAllocator.getResourceCore(), rLimit);
      // Initial values here system dependent, assumes docker container spun up w/ unlimited
      assertEquals(new NativeLong(-1), rLimit.resourceLimitCurrent);
      assertEquals(new NativeLong(-1), rLimit.resourceLimitMaximum);
    }

    libcProtectedMemoryAllocator.disableCoreDumpGlobally();
    assertTrue(libcProtectedMemoryAllocator.areCoreDumpsGloballyDisabled());
    ResourceLimit rLimit = new ResourceLimit();
    ResourceLimit zeroRLimit = ResourceLimit.zero();
    libc.getrlimit(libcProtectedMemoryAllocator.getResourceCore(), rLimit);
    assertEquals(zeroRLimit.resourceLimitCurrent, rLimit.resourceLimitCurrent);
    assertEquals(zeroRLimit.resourceLimitMaximum, rLimit.resourceLimitMaximum);
  }

  @Test
  void testAllocSuccess() {
    Pointer pointer = libcProtectedMemoryAllocator.alloc(1);
    try {
      // just do some sanity checks
      assertNotNull(pointer);
      pointer.setByte(0, (byte) 1);
      assertEquals(1, pointer.getByte(0));
    }
    finally {
      libcProtectedMemoryAllocator.free(pointer, 1);
    }
  }

  @Test
  void testAllocWithSetNoDumpErrorShouldFail() {
    doThrow(LibcOperationFailed.class).when(libcProtectedMemoryAllocator).setNoDump(any(), anyLong());
    assertThrows(LibcOperationFailed.class, () -> libcProtectedMemoryAllocator.alloc(1));
  }

  @Test
  void testAllocWithCheckZeroErrorShouldFail() {
    doThrow(LastErrorException.class).when(libcProtectedMemoryAllocator).checkZero(anyInt(), any());
    assertThrows(LastErrorException.class, () -> libcProtectedMemoryAllocator.alloc(1));
  }

  @Test
  void testFree() {
  }

  @Test
  void testCheckPointerWithRegularPointerShouldSucceed() {
    Pointer pointer = libcProtectedMemoryAllocator.alloc(1);
    try {
      libcProtectedMemoryAllocator.checkPointer(pointer, "blah");
    }
    finally {
      libcProtectedMemoryAllocator.free(pointer, 1);
    }
  }

  @Test
  void testCheckPointerWithNullPointerShouldFail() {
    assertThrows(LibcOperationFailed.class, () -> libcProtectedMemoryAllocator.checkPointer(null, "blah"));
  }

  @Test
  void testCheckPointerWithMapFailedPointerShouldFail() {
    assertThrows(LibcOperationFailed.class,
        () -> libcProtectedMemoryAllocator.checkPointer(Pointer.createConstant(-1), "blah"));
  }

  @Test
  void testCheckZeroWithZeroResult() {
    libcProtectedMemoryAllocator.checkZero(0, "blah");
  }

  @Test
  void testCheckZeroWithNonZeroResult() {
    assertThrows(LibcOperationFailed.class, () ->libcProtectedMemoryAllocator.checkZero(1, "blah"));
  }

  @Test
  void testCheckZeroThrowableWithZeroResult() {
    libcProtectedMemoryAllocator.checkZero(0, "blah", new IllegalStateException());
  }

  @Test
  void testCheckZeroThrowableWithNonZeroResult() {
    assertThrows(LibcOperationFailed.class,
        () ->libcProtectedMemoryAllocator.checkZero(1, "blah", new IllegalStateException()));
  }
}

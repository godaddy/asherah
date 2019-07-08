package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

import static org.junit.jupiter.api.Assertions.*;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.sun.jna.NativeLong;

class ResourceLimitTest {

  @Test
  void testGetFieldOrder() {
    ResourceLimit rLimit = new ResourceLimit();
    List<String> fieldOrder = rLimit.getFieldOrder();
    assertEquals("resourceLimitCurrent", fieldOrder.get(0));
    assertEquals("resourceLimitMaximum", fieldOrder.get(1));
  }

  @Test
  void testZero() {
    ResourceLimit retValue = ResourceLimit.zero();
    NativeLong zeroNativeLong = new NativeLong(0);
    assertEquals(zeroNativeLong, retValue.resourceLimitCurrent);
    assertEquals(zeroNativeLong, retValue.resourceLimitMaximum);
  }

}

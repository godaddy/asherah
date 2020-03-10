package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

import com.sun.jna.NativeLong;
import com.sun.jna.Structure;
import java.util.Arrays;
import java.util.List;

@SuppressWarnings("all")
public class ResourceLimit extends Structure {
  public static final long UNLIMITED = -1;

  public NativeLong resourceLimitCurrent;
  public NativeLong resourceLimitMaximum;

  protected List<String> getFieldOrder() {
    return Arrays.asList("resourceLimitCurrent", "resourceLimitMaximum");
  }

  public static ResourceLimit zero() {
    ResourceLimit rLimit = new ResourceLimit();
    rLimit.resourceLimitCurrent = new NativeLong(0);
    rLimit.resourceLimitMaximum = new NativeLong(0);
    return rLimit;
  }
}

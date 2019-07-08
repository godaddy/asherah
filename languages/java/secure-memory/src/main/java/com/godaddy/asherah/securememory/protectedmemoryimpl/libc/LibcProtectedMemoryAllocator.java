package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.Debug;
import com.godaddy.asherah.securememory.protectedmemoryimpl.MemoryLimitException;
import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemoryAllocator;
import com.sun.jna.NativeLong;
import com.sun.jna.Pointer;

@SuppressWarnings("unused")
public abstract class LibcProtectedMemoryAllocator implements ProtectedMemoryAllocator {
  private static final Logger LOG = LoggerFactory.getLogger(LibcProtectedMemoryAllocator.class);

  protected static final NativeLong Zero = new NativeLong(0);
  private static final Pointer MapFailed = Pointer.createConstant(-1);

  private final NativeLibc libc;

  protected LibcProtectedMemoryAllocator(final NativeLibc libc) {
    this.libc = libc;
  }

  //************************************
  // Memory protection
  //************************************

  protected abstract int getProtRead();

  protected abstract int getProtReadWrite();

  protected abstract int getProtNoAccess();

  protected abstract int getPrivateAnonymousFlags();

  protected abstract int getMemLockLimit();

  @Override
  public void setNoAccess(final Pointer pointer, final long length) {
    checkZero(libc.mprotect(pointer, new NativeLong(length), getProtNoAccess()), "mprotect(PROT_NONE)");
  }

  @Override
  public void setReadAccess(final Pointer pointer, final long length) {
    checkZero(libc.mprotect(pointer, new NativeLong(length), getProtRead()), "mprotect(PROT_READ)");
  }

  @Override
  public void setReadWriteAccess(final Pointer pointer, final long length) {
    checkZero(libc.mprotect(pointer, new NativeLong(length), getProtReadWrite()), "mprotect(PROT_READ | PROT_WRITE)");
  }

  //************************************
  // Core dumps
  //************************************

  private Boolean globallyDisabledCoreDumps = false;

  protected abstract int getResourceCore();

  protected abstract void setNoDump(Pointer pointer, long length);

  protected Boolean areCoreDumpsGloballyDisabled() {
    return globallyDisabledCoreDumps;
  }

  protected void disableCoreDumpGlobally() {
    checkZero(libc.setrlimit(getResourceCore(), ResourceLimit.zero()), "setrlimit(RLIMIT_CORE)");

    globallyDisabledCoreDumps = true;
  }

  //************************************
  // alloc / free
  //************************************

  @Override
  public Pointer alloc(final long length) {
    if (Debug.ON) {
      LOG.debug("attempting to alloc length {}", length);
    }

    NativeLong nLength = new NativeLong(length);

    // Check requested length against rlimit max memlock size
    ResourceLimit resourceLimit = new ResourceLimit();
    libc.getrlimit(getMemLockLimit(), resourceLimit);
    if (resourceLimit.resourceLimitMaximum.longValue() != ResourceLimit.UNLIMITED
        && resourceLimit.resourceLimitMaximum.longValue() < length) {
      throw new MemoryLimitException(String.format(
              "Requested MemLock length exceeds resource limit max of:%d",
              resourceLimit.resourceLimitMaximum.longValue()));
    }

    // Some platforms may require fd to be -1 even if using anonymous
    Pointer protectedMemory = libc.mmap(Pointer.NULL, nLength, getProtReadWrite(), getPrivateAnonymousFlags(),
        -1, Zero);

    checkPointer(protectedMemory, "mmap");

    try {
      checkZero(libc.mlock(protectedMemory, nLength), "mlock");

      try {
        setNoDump(protectedMemory, length);
      }
      catch (Exception t) {
        checkZero(libc.munlock(protectedMemory, nLength), "munlock", t);
        throw t;
      }
    }
    catch (Exception t) {
      checkZero(libc.munmap(protectedMemory, nLength), "munmap", t);
      throw t;
    }
    return protectedMemory;
  }

  @Override
  public void free(final Pointer pointer, final long length) {
    NativeLong nLength = new NativeLong(length);

    try {
      //Wipe the protected memory (assumes memory was made writeable)
      // TODO Not clear if Pointer.clear/Native.setMemory underlying memset call could be optimized away.
      zeroMemory(pointer, length);
    }
    finally {
      try {
        //Regardless of whether or not we successfully wipe, unlock

        //Unlock the protected memory
        checkZero(libc.munlock(pointer, nLength), "munlock");
      }
      finally {
        //Regardless of whether or not we successfully unlock, unmap

        //Free (unmap) the protected memory
        checkZero(libc.munmap(pointer, nLength), "munmap");
      }
    }
  }

  //************************************
  // Libc results checks to exceptions
  //************************************

  protected void checkPointer(final Pointer pointer, final String methodName) {
    if (pointer == Pointer.NULL || pointer.equals(MapFailed)) {
      throw new LibcOperationFailed(methodName, Pointer.nativeValue(pointer));
    }
  }

  protected void checkZero(final int result, final String methodName) {
    if (result != 0) {
      throw new LibcOperationFailed(methodName, result);
    }
  }

  protected void checkZero(final int result, final String methodName, final Throwable t) {
    if (result != 0) {
      throw new LibcOperationFailed(methodName, result, t);
    }
  }
}

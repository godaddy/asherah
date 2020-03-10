package com.godaddy.asherah.securememory.protectedmemoryimpl.libc;

public class LibcOperationFailed extends RuntimeException {
  public LibcOperationFailed(final String operationName, final long result) {
    super("Libc call " + operationName + " failed with result " + result);
  }

  public LibcOperationFailed(final String operationName, final long result, final Throwable t) {
    super("Libc call " + operationName + " failed with result " + result, t);
  }
}

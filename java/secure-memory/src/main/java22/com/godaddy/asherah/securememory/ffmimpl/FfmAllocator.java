package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.MemorySegment;

/**
 * Interface for FFM-based protected memory allocation.
 * This is the FFM equivalent of ProtectedMemoryAllocator.
 */
public interface FfmAllocator {

  /**
   * Allocates protected memory of the specified length.
   *
   * @param length the number of bytes to allocate
   * @return a MemorySegment representing the allocated memory
   */
  MemorySegment alloc(long length);

  /**
   * Frees the protected memory.
   *
   * @param segment the memory segment to free
   * @param length the length of the memory
   */
  void free(MemorySegment segment, long length);

  /**
   * Sets the memory to read-write access.
   *
   * @param segment the memory segment
   * @param length the length of the memory
   */
  void setReadWriteAccess(MemorySegment segment, long length);

  /**
   * Sets the memory to read-only access.
   *
   * @param segment the memory segment
   * @param length the length of the memory
   */
  void setReadAccess(MemorySegment segment, long length);

  /**
   * Sets the memory to no access (protected).
   *
   * @param segment the memory segment
   * @param length the length of the memory
   */
  void setNoAccess(MemorySegment segment, long length);

  /**
   * Securely zeros the memory.
   *
   * @param segment the memory segment
   * @param length the length of the memory
   */
  void zeroMemory(MemorySegment segment, long length);
}


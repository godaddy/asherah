use crate::error::{Result, SecureMemoryError};
use memcall::{self, MemoryProtection};
use std::ptr;
use zeroize::Zeroize;
use log::error;

/// Memory alignment and protection state
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum ProtectionState {
    /// Memory cannot be read or written (default)
    NoAccess,
    /// Memory can be read but not written
    ReadOnly,
    /// Memory can be read and written
    ReadWrite,
}

impl From<ProtectionState> for MemoryProtection {
    fn from(state: ProtectionState) -> Self {
        match state {
            ProtectionState::NoAccess => MemoryProtection::NoAccess,
            ProtectionState::ReadOnly => MemoryProtection::ReadOnly,
            ProtectionState::ReadWrite => MemoryProtection::ReadWrite,
        }
    }
}

/// A wrapper for page-aligned memory with protection capabilities
pub struct AlignedMemory {
    /// Pointer to the allocated memory
    ptr: *mut u8,
    /// Logical size of the data
    len: usize,
    /// Size of the allocated memory (may be larger than len due to alignment)
    capacity: usize,
    /// Current protection state
    protection: ProtectionState,
    /// Whether memory is locked in RAM
    locked: bool,
}

impl AlignedMemory {
    /// Allocates a new page-aligned memory buffer
    ///
    /// # Arguments
    ///
    /// * `size` - The logical size of the data to store
    ///
    /// # Returns
    ///
    /// A new `AlignedMemory` instance with `NoAccess` protection
    pub fn new(size: usize) -> Result<Self> {
        let page_size = memcall::page_size();
        let aligned_size = ((size + page_size - 1) / page_size) * page_size;

        let ptr = memcall::allocate_aligned(aligned_size, page_size)
            .map_err(|e| SecureMemoryError::AllocationFailed(e.to_string()))?;

        Ok(Self {
            ptr,
            len: size,
            capacity: aligned_size,
            protection: ProtectionState::ReadWrite, // Start as writable
            locked: false,
        })
    }

    /// Creates an AlignedMemory from existing data
    pub fn from_data(data: &[u8]) -> Result<Self> {
        let mut memory = Self::new(data.len())?;
        memory.as_mut_slice().copy_from_slice(data);
        Ok(memory)
    }

    /// Locks memory to prevent swapping
    #[cfg(not(feature = "no-mlock"))]
    pub fn lock(&mut self) -> Result<()> {
        if self.locked {
            return Ok(());
        }

        memcall::lock(self.as_mut_slice())
            .map_err(|e| SecureMemoryError::MemoryLockFailed(e.to_string()))?;

        self.locked = true;
        Ok(())
    }

    /// Unlocks previously locked memory
    #[cfg(not(feature = "no-mlock"))]
    pub fn unlock(&mut self) -> Result<()> {
        if !self.locked {
            return Ok(());
        }

        memcall::unlock(self.as_mut_slice())
            .map_err(|e| SecureMemoryError::MemoryLockFailed(e.to_string()))?;

        self.locked = false;
        Ok(())
    }

    /// Sets the protection state of the memory
    pub fn protect(&mut self, state: ProtectionState) -> Result<()> {
        if self.protection == state {
            return Ok(()); // Already in the requested state
        }

        let protection: MemoryProtection = state.into();
        let result = memcall::protect(self.as_slice_for_protection(), protection);

        if let Err(e) = result {
            return Err(SecureMemoryError::ProtectionFailed(e.to_string()));
        }

        self.protection = state;
        Ok(())
    }

    /// Gets a read-only slice to the memory
    ///
    /// # Safety
    ///
    /// This method does not change the protection state of the memory.
    /// The caller must ensure that the memory is readable.
    pub fn as_slice(&self) -> &[u8] {
        unsafe { std::slice::from_raw_parts(self.ptr, self.len) }
    }

    /// Gets a mutable slice to the memory
    ///
    /// # Safety
    ///
    /// This method does not change the protection state of the memory.
    /// The caller must ensure that the memory is writable.
    pub fn as_mut_slice(&mut self) -> &mut [u8] {
        unsafe { std::slice::from_raw_parts_mut(self.ptr, self.len) }
    }

    /// Gets a slice covering the entire capacity for protection operations
    fn as_slice_for_protection(&self) -> &[u8] {
        unsafe { std::slice::from_raw_parts(self.ptr, self.capacity) }
    }

    /// Securely zeroes the memory
    pub fn zeroize(&mut self) -> Result<()> {
        // Ensure we can write to the memory
        let original_protection = self.protection;
        if original_protection != ProtectionState::ReadWrite {
            self.protect(ProtectionState::ReadWrite)?;
        }

        // Zero the memory
        self.as_mut_slice().zeroize();

        // Restore original protection if needed
        if original_protection != ProtectionState::ReadWrite {
            if let Err(e) = self.protect(original_protection) {
                error!("Failed to restore memory protection after zeroing: {}", e);
                // Continue with drop - we've zeroed memory which is most important
            }
        }

        Ok(())
    }

    /// Returns the length of the data
    pub fn len(&self) -> usize {
        self.len
    }

    /// Returns true if the memory is empty
    pub fn is_empty(&self) -> bool {
        self.len == 0
    }

    /// Returns the capacity of the allocated memory
    pub fn capacity(&self) -> usize {
        self.capacity
    }

    /// Returns the current protection state
    pub fn protection(&self) -> ProtectionState {
        self.protection
    }
}

impl Drop for AlignedMemory {
    fn drop(&mut self) {
        if self.ptr.is_null() {
            return;
        }

        // Best effort to make memory writable for zeroing
        if self.protection != ProtectionState::ReadWrite {
            let _ = self.protect(ProtectionState::ReadWrite);
        }

        // Zero the memory
        self.as_mut_slice().zeroize();

        // Unlock if locked
        #[cfg(not(feature = "no-mlock"))]
        if self.locked {
            let _ = self.unlock();
        }

        // Free the memory
        unsafe {
            if let Err(e) = memcall::free_aligned(self.ptr, self.capacity) {
                error!("Failed to free aligned memory: {}", e);
            }
        }

        // Prevent double-free
        self.ptr = ptr::null_mut();
    }
}

// AlignedMemory can be sent between threads
unsafe impl Send for AlignedMemory {}
// AlignedMemory can be shared between threads
unsafe impl Sync for AlignedMemory {}

/// A custom allocator that ensures page-aligned allocations
pub struct AlignedAllocator;

impl AlignedAllocator {
    /// Create a page-aligned allocation using memcall
    pub fn alloc_aligned(size: usize) -> Result<*mut u8, &'static str> {
        let page_size = memcall::page_size();
        let aligned_size = ((size + page_size - 1) / page_size) * page_size;

        memcall::allocate_aligned(aligned_size, page_size)
            .map_err(|_| "Allocation failed")
    }

    /// Deallocate a page-aligned allocation using memcall
    pub unsafe fn dealloc_aligned(ptr: *mut u8, size: usize) -> Result<(), &'static str> {
        let page_size = memcall::page_size();
        let aligned_size = ((size + page_size - 1) / page_size) * page_size;

        memcall::free_aligned(ptr, aligned_size)
            .map_err(|_| "Deallocation failed")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_aligned_memory_new() {
        let memory = AlignedMemory::new(100).unwrap();
        assert_eq!(memory.len(), 100);
        assert!(memory.capacity() >= 100);
        assert_eq!(memory.protection(), ProtectionState::ReadWrite);
    }

    #[test]
    fn test_aligned_memory_from_data() {
        let data = vec![1, 2, 3, 4, 5];
        let memory = AlignedMemory::from_data(&data).unwrap();

        // Change to ReadOnly to verify data was copied
        let mut memory = memory;
        memory.protect(ProtectionState::ReadOnly).unwrap();

        assert_eq!(memory.as_slice(), &[1, 2, 3, 4, 5]);
        assert_eq!(memory.len(), 5);
    }

    #[test]
    fn test_aligned_memory_protection() {
        let mut memory = AlignedMemory::new(100).unwrap();

        // Test protection transitions
        memory.protect(ProtectionState::NoAccess).unwrap();
        assert_eq!(memory.protection(), ProtectionState::NoAccess);

        memory.protect(ProtectionState::ReadOnly).unwrap();
        assert_eq!(memory.protection(), ProtectionState::ReadOnly);

        memory.protect(ProtectionState::ReadWrite).unwrap();
        assert_eq!(memory.protection(), ProtectionState::ReadWrite);
    }

    #[test]
    fn test_aligned_memory_zeroize() {
        let data = vec![1, 2, 3, 4, 5];
        let mut memory = AlignedMemory::from_data(&data).unwrap();

        // Test zeroing
        memory.zeroize().unwrap();

        // Make readable to verify
        memory.protect(ProtectionState::ReadOnly).unwrap();

        // Verify zeroed
        for &byte in memory.as_slice() {
            assert_eq!(byte, 0);
        }
    }

    #[test]
    fn test_aligned_memory_drop() {
        let data = vec![0xFFu8; 100];
        let ptr = {
            let memory = AlignedMemory::from_data(&data).unwrap();
            // Capture the pointer address
            memory.ptr
        };
        // Memory should now be dropped and zeroed

        // We can't directly verify the memory was zeroed as accessing it would be unsafe
        // This is mainly to verify no panics occur during drop
        assert!(true);
    }
}
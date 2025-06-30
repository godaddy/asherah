use crate::error::{Result, SecureMemoryError};
use crate::secret::{Secret, SecretExtensions};
use log::{debug, trace};
use memcall::{self, MemoryProtection};
use std::io::Read;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use std::sync::{Arc, Condvar, Mutex};

/// State of memory protection
#[derive(Debug, Clone, Copy)]
enum ProtectionState {
    NoAccess,
    ReadOnly,
    ReadWrite,
}

/// Internal state of a protected memory secret
struct SecretInternal {
    // Memory management
    ptr: *mut u8,
    len: usize,
    capacity: usize,

    // State tracking
    closed: AtomicBool,
    closing: AtomicBool,
    freed: AtomicBool, // Track if memory has been freed

    // Access control
    access_count: AtomicUsize,
    access_mutex: Mutex<()>, // Mutex for the condition variable
    access_cond: Condvar,    // Notify when access count changes

    // Protection state (separate mutex to avoid deadlocks)
    protection: Mutex<ProtectionState>,
}

// Safety: ptr points to allocated memory that we own
unsafe impl Send for SecretInternal {}
unsafe impl Sync for SecretInternal {}

/// A secret implementation that uses protected memory to store sensitive data.
pub struct ProtectedMemorySecretSimple {
    inner: Arc<SecretInternal>,
}

impl ProtectedMemorySecretSimple {
    /// Creates a new ProtectedMemorySecret from provided data
    pub fn new(data: &[u8]) -> Result<Self> {
        let len = data.len();
        if len == 0 {
            return Err(SecureMemoryError::OperationFailed(
                "Cannot create a secret with zero length".to_string(),
            ));
        }

        trace!("Creating new ProtectedMemorySecret with {} bytes", len);

        // Allocate page-aligned memory
        let page_size = memcall::page_size();
        // We need to manually calculate the ceiling of len/page_size
        #[allow(clippy::manual_div_ceil)]
        let aligned_size = ((len + page_size - 1) / page_size) * page_size;

        let memory_ptr = memcall::allocate_aligned(aligned_size, page_size)
            .map_err(|e| SecureMemoryError::AllocationFailed(e.to_string()))?;

        // Copy data to the allocated memory
        unsafe {
            std::ptr::copy_nonoverlapping(data.as_ptr(), memory_ptr, len);
        }

        // Lock memory to prevent swapping
        #[cfg(not(feature = "no-mlock"))]
        {
            let slice = unsafe { std::slice::from_raw_parts_mut(memory_ptr, len) };
            memcall::lock(slice).map_err(|e| SecureMemoryError::MemoryLockFailed(e.to_string()))?;
        }

        // Initial protection - protect the entire allocated space
        let full_slice = unsafe { std::slice::from_raw_parts_mut(memory_ptr, aligned_size) };
        memcall::protect(full_slice, MemoryProtection::NoAccess)
            .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

        Ok(Self {
            inner: Arc::new(SecretInternal {
                ptr: memory_ptr,
                len,
                capacity: aligned_size,
                closed: AtomicBool::new(false),
                closing: AtomicBool::new(false),
                freed: AtomicBool::new(false),
                access_count: AtomicUsize::new(0),
                access_mutex: Mutex::new(()),
                access_cond: Condvar::new(),
                protection: Mutex::new(ProtectionState::NoAccess),
            }),
        })
    }

    /// Check if we can enable read access (helper method)
    fn enable_read_access(&self) -> Result<()> {
        // Check if memory has been freed
        if self.inner.freed.load(Ordering::Acquire) {
            return Err(SecureMemoryError::SecretClosed);
        }

        // Only change protection if we're the first accessor
        if self.inner.access_count.load(Ordering::Acquire) != 1 {
            return Ok(());
        }

        // Check for null pointer (defensive)
        if self.inner.ptr.is_null() {
            return Err(SecureMemoryError::OperationFailed(
                "Memory pointer is null".to_string(),
            ));
        }

        let mut protection = self.inner.protection.lock().map_err(|_| {
            SecureMemoryError::OperationFailed("Protection lock poisoned".to_string())
        })?;

        // Check again after acquiring lock
        if self.inner.freed.load(Ordering::Acquire) || self.inner.ptr.is_null() {
            return Err(SecureMemoryError::SecretClosed);
        }

        // Capture local copy of ptr and capacity to avoid races
        let ptr = self.inner.ptr;
        let capacity = self.inner.capacity;

        if matches!(*protection, ProtectionState::NoAccess) {
            let full_slice = unsafe { std::slice::from_raw_parts_mut(ptr, capacity) };

            memcall::protect(full_slice, MemoryProtection::ReadOnly)
                .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

            *protection = ProtectionState::ReadOnly;
        }

        Ok(())
    }

    /// Restore no-access protection (helper method)
    fn disable_access(&self) -> Result<()> {
        // Only change protection if we're the last accessor
        if self.inner.access_count.load(Ordering::Acquire) != 0 {
            return Ok(());
        }

        let mut protection = self.inner.protection.lock().map_err(|_| {
            SecureMemoryError::OperationFailed("Protection lock poisoned".to_string())
        })?;

        if !matches!(*protection, ProtectionState::NoAccess) {
            let full_slice =
                unsafe { std::slice::from_raw_parts_mut(self.inner.ptr, self.inner.capacity) };

            memcall::protect(full_slice, MemoryProtection::NoAccess)
                .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

            *protection = ProtectionState::NoAccess;
        }

        Ok(())
    }

    /// Clean up memory (helper for close)
    fn cleanup_memory(&self) -> Result<()> {
        // Check if already freed
        if self
            .inner
            .freed
            .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
            .is_err()
        {
            return Ok(()); // Already freed
        }

        // Capture current pointer and size information for consistent access
        let ptr = self.inner.ptr;
        let capacity = self.inner.capacity;
        let len = self.inner.len;

        // Only clean up if we have valid memory
        if ptr.is_null() || capacity == 0 {
            return Ok(());
        }

        // Enable write access for cleanup
        {
            let mut protection = self.inner.protection.lock().map_err(|_| {
                SecureMemoryError::OperationFailed("Protection lock poisoned".to_string())
            })?;

            // Re-check pointer hasn't been nulled by another thread
            if ptr.is_null() {
                return Ok(());
            }

            let full_slice = unsafe { std::slice::from_raw_parts_mut(ptr, capacity) };

            memcall::protect(full_slice, MemoryProtection::ReadWrite)
                .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

            *protection = ProtectionState::ReadWrite;
        }

        // Unlock memory
        #[cfg(not(feature = "no-mlock"))]
        if len > 0 {
            let slice = unsafe { std::slice::from_raw_parts_mut(ptr, len) };
            let _ = memcall::unlock(slice);
        }

        // Wipe memory
        unsafe {
            std::ptr::write_bytes(ptr, 0, capacity);
        }

        // Free the page-aligned memory
        unsafe {
            let _ = memcall::free_aligned(ptr, capacity);
        }

        // Note about setting ptr to null:
        // We don't need to set the ptr to null here since:
        // 1. We've already marked the memory as freed with the atomic flag
        // 2. All code paths check the freed flag before using the pointer
        // 3. Directly setting the inner.ptr would require unsafe interior mutability
        // 4. When SecretInternal is dropped, it will set ptr to null if needed

        Ok(())
    }
}

impl Secret for ProtectedMemorySecretSimple {
    fn len(&self) -> usize {
        self.inner.len
    }

    fn is_closed(&self) -> bool {
        self.inner.closed.load(Ordering::Acquire)
    }

    fn reader(&self) -> Result<Box<dyn Read + Send + Sync + '_>> {
        Ok(Box::new(SecretReader {
            secret: self,
            position: 0,
        }))
    }

    fn close(&self) -> Result<()> {
        // Step 1: Mark as closing (atomic CAS)
        if self
            .inner
            .closing
            .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
            .is_err()
        {
            return Ok(()); // Already closing
        }

        // Step 2: Mark as closed to prevent new accessors
        self.inner.closed.store(true, Ordering::Release);

        // Step 3: Wait for existing accessors to finish using condition variable
        // First acquire the access mutex (separate from protection mutex)
        let mut guard =
            self.inner.access_mutex.lock().map_err(|_| {
                SecureMemoryError::OperationFailed("Access mutex poisoned".to_string())
            })?;

        let mut wait_count = 0;
        loop {
            let count = self.inner.access_count.load(Ordering::Acquire);
            if count == 0 {
                break;
            }

            wait_count += 1;
            if wait_count % 100 == 0 {
                debug!(
                    "Waiting for {} accessors to finish (iteration {})",
                    count, wait_count
                );
            }

            guard = self.inner.access_cond.wait(guard).map_err(|_| {
                SecureMemoryError::OperationFailed("Condition variable wait failed".to_string())
            })?;
        }

        // Release the access mutex before acquiring the protection mutex to avoid deadlocks
        drop(guard);

        // Step 4: Perform cleanup with exclusive access
        self.cleanup_memory()
    }
}

impl SecretExtensions for ProtectedMemorySecretSimple {
    fn with_bytes<F, T>(&self, f: F) -> Result<T>
    where
        F: FnOnce(&[u8]) -> Result<T>,
    {
        // Step 1: Fast-path check for closed state or freed memory
        if self.inner.closed.load(Ordering::Acquire) || self.inner.freed.load(Ordering::Acquire) {
            return Err(SecureMemoryError::SecretClosed);
        }

        // Step 2: Increment access counter
        let old_count = self.inner.access_count.fetch_add(1, Ordering::AcqRel);
        trace!(
            "Access count increased from {} to {}",
            old_count,
            old_count + 1
        );

        // Ensure we decrement the counter on all exit paths
        let _guard = AccessGuard { inner: &self.inner };

        // Step 3: Check again after incrementing (could have closed meanwhile or memory freed)
        if self.inner.closed.load(Ordering::Acquire) || self.inner.freed.load(Ordering::Acquire) {
            return Err(SecureMemoryError::SecretClosed);
        }

        // Check if pointer is null (defensive)
        if self.inner.ptr.is_null() {
            return Err(SecureMemoryError::OperationFailed(
                "Memory pointer is null".to_string(),
            ));
        }

        // Step 4: Enable read access if needed
        self.enable_read_access()?;

        // Step 5: Capture pointer and length locally to avoid races
        let ptr = self.inner.ptr;
        let len = self.inner.len;

        // Final check that memory wasn't freed while enabling access
        if self.inner.freed.load(Ordering::Acquire) || ptr.is_null() {
            return Err(SecureMemoryError::SecretClosed);
        }

        // Create slice and execute user callback
        let data_slice = unsafe { std::slice::from_raw_parts(ptr, len) };

        f(data_slice)
    }

    fn with_bytes_func<F, R>(&self, f: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<(R, Vec<u8>)>,
    {
        self.with_bytes(|data| {
            let (result, _bytes) = f(data)?;
            Ok(result)
        })
    }
}

/// RAII guard for access counting
struct AccessGuard<'guard> {
    inner: &'guard SecretInternal,
}

impl Drop for AccessGuard<'_> {
    fn drop(&mut self) {
        // Decrement access counter
        let old_count = self.inner.access_count.fetch_sub(1, Ordering::AcqRel);
        let was_last = old_count == 1;
        trace!(
            "Access count decreased from {} to {}",
            old_count,
            old_count - 1
        );

        // Notify waiters if we were the last accessor
        if was_last {
            // Acquire access mutex before signaling
            if let Ok(guard) = self.inner.access_mutex.lock() {
                self.inner.access_cond.notify_all();
                // Release the lock immediately
                drop(guard);
            }
        }

        // If we were the last accessor and not closing, restore protection
        if was_last && !self.inner.closing.load(Ordering::Acquire) {
            // First check if the memory has been freed to avoid use-after-free
            if !self.inner.freed.load(Ordering::Acquire) {
                // Acquire the protection lock to update protection state
                if let Ok(mut protection) = self.inner.protection.lock() {
                    // Double-check freed state after acquiring lock to avoid race condition
                    if !matches!(*protection, ProtectionState::NoAccess)
                        && !self.inner.ptr.is_null()
                        && !self.inner.freed.load(Ordering::Acquire)
                    {
                        let full_slice = unsafe {
                            std::slice::from_raw_parts_mut(self.inner.ptr, self.inner.capacity)
                        };

                        // Ignore protection errors during cleanup
                        let _ = memcall::protect(full_slice, MemoryProtection::NoAccess);

                        *protection = ProtectionState::NoAccess;
                    }
                }
            }
        }
    }
}

impl Clone for ProtectedMemorySecretSimple {
    fn clone(&self) -> Self {
        Self {
            inner: Arc::clone(&self.inner),
        }
    }
}

impl Drop for ProtectedMemorySecretSimple {
    fn drop(&mut self) {
        if Arc::strong_count(&self.inner) == 1 && !self.is_closed() {
            debug!("Last reference to secret being dropped, closing");
            let _ = self.close();
        }
    }
}

// Custom drop for SecretInternal to handle any cleanup we missed
impl Drop for SecretInternal {
    fn drop(&mut self) {
        // Only cleanup if memory hasn't been freed yet
        if !self.freed.load(Ordering::Acquire) && !self.ptr.is_null() && self.capacity > 0 {
            debug!("SecretInternal drop: performing emergency cleanup");

            // Mark as freed to prevent double free
            if self
                .freed
                .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
                .is_err()
            {
                return; // Already freed by another thread
            }

            // Capture pointer and capacity values before any operations that might invalidate them
            let ptr = self.ptr;
            let capacity = self.capacity;
            let len = self.len;

            // Enable write access for final cleanup
            let full_slice = unsafe { std::slice::from_raw_parts_mut(ptr, capacity) };
            let _ = memcall::protect(full_slice, MemoryProtection::ReadWrite);

            // Unlock memory
            #[cfg(not(feature = "no-mlock"))]
            if len > 0 {
                let slice = unsafe { std::slice::from_raw_parts_mut(ptr, len) };
                let _ = memcall::unlock(slice);
            }

            // Wipe memory
            unsafe {
                std::ptr::write_bytes(ptr, 0, capacity);
            }

            // Free the page-aligned memory
            unsafe {
                let _ = memcall::free_aligned(ptr, capacity);
            }

            // Set ptr to null to prevent any further access attempts
            self.ptr = std::ptr::null_mut();
        }
    }
}

/// A reader implementation for protected memory secrets
struct SecretReader<'secret> {
    secret: &'secret ProtectedMemorySecretSimple,
    position: usize,
}

impl Read for SecretReader<'_> {
    fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
        self.secret
            .with_bytes(|bytes| {
                if self.position >= bytes.len() {
                    return Ok(0);
                }

                let remaining = bytes.len() - self.position;
                let to_read = remaining.min(buf.len());
                buf[..to_read].copy_from_slice(&bytes[self.position..self.position + to_read]);
                self.position += to_read;
                Ok(to_read)
            })
            .map_err(|e| std::io::Error::new(std::io::ErrorKind::Other, e))
    }
}

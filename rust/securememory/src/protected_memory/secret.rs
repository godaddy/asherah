use crate::error::{Result, SecureMemoryError};
use crate::secret::{Secret, SecretExtensions};
use log::{error, trace, warn};
use memcall::{self, MemoryProtection};
use std::io::Read;
use std::sync::{Arc, Condvar, Mutex, OnceLock, RwLock, Weak};
use zeroize::Zeroize;

// Global registry for all ProtectedMemorySecret instances
// Stores Weak pointers to avoid preventing secrets from being dropped.
pub(crate) static SECRET_REGISTRY: OnceLock<Mutex<Vec<Weak<SecretInternal>>>> = OnceLock::new();

/// Wrapper for memory that handles proper deallocation
struct AlignedMemory {
    ptr: *mut u8,
    len: usize,
    capacity: usize,
}

impl AlignedMemory {
    fn new(size: usize) -> Result<Self> {
        let page_size = memcall::page_size();
        let aligned_size = ((size + page_size - 1) / page_size) * page_size;

        let ptr = memcall::allocate_aligned(aligned_size, page_size)
            .map_err(|e| SecureMemoryError::AllocationFailed(e.to_string()))?;

        Ok(Self {
            ptr,
            len: size,
            capacity: aligned_size,
        })
    }

    fn as_slice(&self) -> &[u8] {
        unsafe { std::slice::from_raw_parts(self.ptr, self.len) }
    }

    fn as_mut_slice(&mut self) -> &mut [u8] {
        unsafe { std::slice::from_raw_parts_mut(self.ptr, self.len) }
    }

    /// Get a mutable slice for memory protection operations
    /// This is safe because we control access through the lock
    unsafe fn as_mut_slice_for_protection(&self) -> &mut [u8] {
        std::slice::from_raw_parts_mut(self.ptr, self.len)
    }
}

impl Drop for AlignedMemory {
    fn drop(&mut self) {
        if !self.ptr.is_null() {
            // Use a raw slice to avoid issues during drop
            let slice = unsafe { std::slice::from_raw_parts_mut(self.ptr, self.capacity) };

            // Make sure memory is in a state where it can be deallocated
            if let Err(e) = memcall::protect(slice, MemoryProtection::ReadWrite) {
                // On error, we can't do much except log it
                eprintln!("Failed to unprotect memory before deallocation: {}", e);
            }

            #[cfg(not(feature = "no-mlock"))]
            if let Err(e) = memcall::unlock(slice) {
                eprintln!("Failed to unlock memory before deallocation: {}", e);
            }

            unsafe {
                if let Err(e) = memcall::free_aligned(self.ptr, self.capacity) {
                    eprintln!("Failed to free aligned memory: {}", e);
                }
            }

            // Mark the pointer as null to prevent double-free
            self.ptr = std::ptr::null_mut();
        }
    }
}

// Safety: AlignedMemory owns its memory exclusively and controls access
unsafe impl Send for AlignedMemory {}
unsafe impl Sync for AlignedMemory {}

/// Internal state of a protected memory secret
pub(crate) struct SecretInternal {
    // Using std RwLock to match Go's sync.RWMutex
    bytes: RwLock<Option<AlignedMemory>>,
    // Lock and Condvar for coordinating close (matching Go's mutex + cond)
    rw: Mutex<InternalState>,
    condvar: Condvar,
}

#[derive(Debug)]
struct InternalState {
    closed: bool,
    closing: bool,
    access_counter: usize,
}

/// A secret implementation that uses protected memory to store sensitive data.
///
/// This implementation:
/// - Uses platform memory protection to control access
/// - Prevents swapping to disk via memory locking
/// - Provides thread-safe access
/// - Securely wipes memory on cleanup
/// - Optimized for performance while maintaining safety
pub struct ProtectedMemorySecret {
    // Core state - single Arc like Go's pointer
    pub(crate) inner: Arc<SecretInternal>,
}

impl ProtectedMemorySecret {
    /// Creates a new ProtectedMemorySecret from provided data
    pub fn new(data: &[u8]) -> Result<Self> {
        let len = data.len();
        if len == 0 {
            return Err(SecureMemoryError::OperationFailed(
                "Cannot create a secret with zero length".to_string(),
            ));
        }

        trace!("Creating new ProtectedMemorySecret with {} bytes", len);

        // Create aligned memory and copy data
        let mut memory = AlignedMemory::new(len)?;
        memory.as_mut_slice().copy_from_slice(data);

        // Lock memory
        #[cfg(not(feature = "no-mlock"))]
        memcall::lock(memory.as_mut_slice())
            .map_err(|e| SecureMemoryError::MemoryLockFailed(e.to_string()))?;

        // Initial protection
        memcall::protect(memory.as_mut_slice(), MemoryProtection::NoAccess)
            .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

        let new_secret_internal = Arc::new(SecretInternal {
            bytes: RwLock::new(Some(memory)),
            rw: Mutex::new(InternalState {
                closed: false,
                closing: false,
                access_counter: 0,
            }),
            condvar: Condvar::new(),
        });

        // Register the new secret
        let registry = SECRET_REGISTRY.get_or_init(|| Mutex::new(Vec::new()));
        if let Ok(mut reg_guard) = registry.lock() {
            // Prune dead weak pointers before adding a new one
            reg_guard.retain(|weak_ref| weak_ref.strong_count() > 0);
            reg_guard.push(Arc::downgrade(&new_secret_internal));
        } else {
            error!("Failed to lock SECRET_REGISTRY to register a new secret. This secret will not be auto-purged by signals.");
        }

        Ok(Self {
            inner: new_secret_internal,
        })
    }

    pub fn from_random(len: usize) -> Result<Self> {
        let mut data = vec![0u8; len];
        // Use getrandom to fill the buffer with random bytes
        use getrandom::getrandom;
        getrandom(&mut data).map_err(|e| SecureMemoryError::AllocationFailed(e.to_string()))?;
        let secret = Self::new(&data)?;
        data.zeroize();
        Ok(secret)
    }
}

impl Secret for ProtectedMemorySecret {
    fn len(&self) -> usize {
        // Fast path - no need to lock for length
        if let Ok(guard) = self.inner.bytes.try_read() {
            guard.as_ref().map(|m| m.len).unwrap_or(0)
        } else {
            0 // Fallback, though this shouldn't happen
        }
    }

    fn is_closed(&self) -> bool {
        if let Ok(guard) = self.inner.rw.lock() {
            guard.closed
        } else {
            false
        }
    }

    fn reader(&self) -> Result<Box<dyn Read + Send + Sync + '_>> {
        Ok(Box::new(SecretReader {
            secret: self,
            position: 0,
        }))
    }

    fn close(&self) -> Result<()> {
        self.close_impl()
    }
}

impl SecretExtensions for ProtectedMemorySecret {
    fn with_bytes<F, T>(&self, f: F) -> Result<T>
    where
        F: FnOnce(&[u8]) -> Result<T>,
    {
        // Access (matching Go's access() method)
        {
            let mut guard = self.inner.rw.lock().map_err(|_| {
                SecureMemoryError::OperationFailed("Failed to acquire access lock".to_string())
            })?;

            if guard.closing || guard.closed {
                return Err(SecureMemoryError::SecretClosed);
            }

            // Only set read access if we're the first one trying to access this potentially-shared Secret
            if guard.access_counter == 0 {
                let bytes_guard = self.inner.bytes.read().map_err(|_| {
                    SecureMemoryError::OperationFailed("Failed to acquire read lock".to_string())
                })?;

                if let Some(memory) = bytes_guard.as_ref() {
                    if let Err(e) = memcall::protect(
                        unsafe { memory.as_mut_slice_for_protection() },
                        MemoryProtection::ReadOnly,
                    ) {
                        return Err(SecureMemoryError::ProtectionFailed(e.to_string()));
                    }
                }
            }
            guard.access_counter += 1;
        }

        // Run the function with the bytes
        let result = {
            let guard = self.inner.bytes.read().map_err(|_| {
                SecureMemoryError::OperationFailed("Failed to acquire read lock".to_string())
            })?;

            let memory = guard.as_ref().ok_or(SecureMemoryError::SecretClosed)?;

            f(memory.as_slice())
        };

        // Release (matching Go's release() method)
        {
            let mut guard = self.inner.rw.lock().map_err(|_| {
                SecureMemoryError::OperationFailed("Failed to acquire release lock".to_string())
            })?;

            guard.access_counter -= 1;

            // Only set no access if we're the last one trying to access this potentially-shared secret
            if guard.access_counter == 0 {
                let bytes_guard = self.inner.bytes.read().map_err(|_| {
                    SecureMemoryError::OperationFailed("Failed to acquire read lock".to_string())
                })?;

                if let Some(memory) = bytes_guard.as_ref() {
                    if let Err(e) = memcall::protect(
                        unsafe { memory.as_mut_slice_for_protection() },
                        MemoryProtection::NoAccess,
                    ) {
                        // Don't fail the operation if we can't protect the memory
                        warn!("Failed to set memory to NoAccess: {}", e);
                    }
                }
            }

            // Broadcast to wake up any waiting Close() calls
            self.inner.condvar.notify_all();
        }

        result
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

impl Drop for ProtectedMemorySecret {
    fn drop(&mut self) {
        // Only cleanup if this is the last reference
        if Arc::strong_count(&self.inner) == 1 {
            // Attempt cleanup only once
            if let Err(e) = self.close_impl() {
                // Log the error but don't panic during drop
                eprintln!("Error closing secret during drop: {}", e);
            }
        }
    }
}

impl Clone for ProtectedMemorySecret {
    fn clone(&self) -> Self {
        Self {
            inner: Arc::clone(&self.inner),
        }
    }
}

impl ProtectedMemorySecret {
    /// Manually close the secret - matching Go's behavior
    fn close_impl(&self) -> Result<()> {
        let mut guard = self.inner.rw.lock().map_err(|_| {
            SecureMemoryError::OperationFailed("Failed to acquire close mutex".to_string())
        })?;

        guard.closing = true;

        // Wait for all accessors to finish (matching Go's Close() loop)
        loop {
            if guard.closed {
                return Ok(());
            }

            if guard.access_counter == 0 {
                // Actually perform the close operation
                let mut bytes_guard = self.inner.bytes.write().map_err(|_| {
                    SecureMemoryError::OperationFailed(
                        "Failed to acquire write lock for cleanup".to_string(),
                    )
                })?;

                // Take the memory out of the Option, replacing it with None
                if let Some(mut memory) = bytes_guard.take() {
                    // Enable write access for wiping
                    memcall::protect(memory.as_mut_slice(), MemoryProtection::ReadWrite)
                        .map_err(|e| SecureMemoryError::ProtectionFailed(e.to_string()))?;

                    // Wipe memory
                    memory.as_mut_slice().zeroize();

                    // Memory will be cleaned up by AlignedMemory's Drop implementation
                }

                guard.closed = true;

                // Note: We don't explicitly remove from SECRET_REGISTRY here.
                // The Weak pointers will simply fail to upgrade later, and purge/registration can prune them.

                return Ok(());
            }

            // Wait for accessor to finish
            guard = self.inner.condvar.wait(guard).map_err(|_| {
                SecureMemoryError::OperationFailed("Failed to wait on condvar".to_string())
            })?;
        }
    }
}

/// A reader implementation for protected memory secrets
struct SecretReader<'a> {
    secret: &'a ProtectedMemorySecret,
    position: usize,
}

impl<'a> Read for SecretReader<'a> {
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

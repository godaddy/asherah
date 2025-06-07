# Protected Memory Design

## Overview

The Protected Memory implementation in SecureMemory provides a secure way to store sensitive data in memory with proper access controls, memory protection, and concurrent access safety. The design addresses several key challenges:

1. **Memory Safety**: Preventing unauthorized access to sensitive data
2. **Concurrency**: Supporting safe concurrent access from multiple threads
3. **Resource Management**: Ensuring proper cleanup of sensitive data
4. **Performance**: Minimizing overhead while maintaining security

## Core Architecture

### Memory Management

```rust
struct SecretInternal {
    // Memory management
    ptr: *mut u8,             // Raw pointer to allocated memory
    len: usize,               // Actual data length
    capacity: usize,          // Allocated capacity (page-aligned)
    
    // State tracking
    closed: AtomicBool,       // Whether the secret is closed
    closing: AtomicBool,      // Whether close is in progress
    freed: AtomicBool,        // Whether memory has been freed
    
    // Access control
    access_count: AtomicUsize, // Number of active accessors
    access_mutex: Mutex<()>,   // Mutex for condition variable
    access_cond: Condvar,      // Notifies on access count changes
    
    // Protection state
    protection: Mutex<ProtectionState>, // Current memory protection
}
```

Key operations:
- **Allocation**: Page-aligned memory allocation via `memcall::allocate_aligned`
- **Locking**: Prevent memory from being swapped via `memcall::lock`
- **Protection**: Control memory access via `memcall::protect`
- **Cleanup**: Secure zeroing and deallocation

### State Management

The implementation uses a state machine approach to track memory states:

1. **ProtectionState Enum**:
   ```rust
   enum ProtectionState {
       NoAccess,
       ReadOnly,
       ReadWrite,
   }
   ```

2. **Lifecycle States** (tracked via atomic flags):
   - `closed`: Secret is permanently closed and memory freed
   - `closing`: Close operation is in progress
   - `freed`: Memory has been freed

3. **Access Control**:
   - `access_count`: Tracks number of active readers
   - `access_mutex` + `access_cond`: Coordinates wait/notify for accessors

## Thread Safety Design

### Synchronization Mechanisms

1. **Atomic Operations**:
   - Fast-path state checks via `AtomicBool` and `AtomicUsize`
   - Compare-and-swap for state transitions
   - Acquire/Release ordering for memory visibility

2. **Lock Hierarchy**:
   - `access_mutex`: Controls condition variable operations
   - `protection`: Controls memory protection changes
   - Strict ordering: acquire access mutex before protection mutex

3. **RAII Guards**:
   - `AccessGuard`: Manages access count and cleanup

### Concurrency Patterns

1. **Reader Pattern**:
   ```rust
   fn with_bytes<F, T>(&self, f: F) -> Result<T> {
       // Fast-path checks with atomics
       if self.inner.closed.load(Ordering::Acquire) || self.inner.freed.load(Ordering::Acquire) {
           return Err(SecureMemoryError::SecretClosed);
       }
       
       // Increment access counter
       let old_count = self.inner.access_count.fetch_add(1, Ordering::AcqRel);
       
       // RAII guard ensures decrement on all exit paths
       let _guard = AccessGuard { inner: &self.inner };
       
       // Double-check after incrementing
       if self.inner.closed.load(Ordering::Acquire) || self.inner.freed.load(Ordering::Acquire) {
           return Err(SecureMemoryError::SecretClosed);
       }
       
       // Enable read access if needed
       self.enable_read_access()?;
       
       // Execute user callback with no locks held
       let data_slice = unsafe { std::slice::from_raw_parts(self.inner.ptr, self.inner.len) };
       f(data_slice)
   }
   ```

2. **Close Pattern**:
   ```rust
   fn close(&self) -> Result<()> {
       // Mark as closing (atomic CAS)
       if self.inner.closing.compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire).is_err() {
           return Ok(()); // Already closing
       }
       
       // Mark as closed to prevent new accessors
       self.inner.closed.store(true, Ordering::Release);
       
       // Wait for existing accessors to finish using condition variable
       let mut guard = self.inner.access_mutex.lock()?;
       
       while self.inner.access_count.load(Ordering::Acquire) > 0 {
           guard = self.inner.access_cond.wait(guard)?;
       }
       
       // Release the access mutex before cleanup
       drop(guard);
       
       // Perform cleanup
       self.cleanup_memory()
   }
   ```

### Safety Mechanisms

1. **Defensive Programming**:
   - Check freed state before every memory access
   - Validate pointers before dereferencing
   - Capture local copies of values to avoid races

2. **Double-Check Pattern**:
   - Check state before acquiring locks
   - Re-check state after acquiring locks
   - Validate all invariants before critical operations

3. **Race Condition Prevention**:
   - Clear state transitions with atomic operations
   - Proper notification on state changes
   - Consistent lock ordering

## Resource Management

### AccessGuard Pattern

```rust
struct AccessGuard<'a> {
    inner: &'a SecretInternal,
}

impl<'a> Drop for AccessGuard<'a> {
    fn drop(&mut self) {
        // Decrement access counter
        let old_count = self.inner.access_count.fetch_sub(1, Ordering::AcqRel);
        let was_last = old_count == 1;
        
        // Notify waiters if we were the last accessor
        if was_last {
            if let Ok(guard) = self.inner.access_mutex.lock() {
                self.inner.access_cond.notify_all();
                drop(guard);
            }
        }
        
        // If we were the last accessor, restore protection
        if was_last && !self.inner.closing.load(Ordering::Acquire) {
            if !self.inner.freed.load(Ordering::Acquire) {
                if let Ok(mut protection) = self.inner.protection.lock() {
                    // Double-check freed state after acquiring lock
                    if !matches!(*protection, ProtectionState::NoAccess) && 
                       !self.inner.ptr.is_null() && 
                       !self.inner.freed.load(Ordering::Acquire) {
                        // Restore protection to NoAccess
                        let full_slice = unsafe {
                            std::slice::from_raw_parts_mut(self.inner.ptr, self.inner.capacity)
                        };
                        let _ = memcall::protect(full_slice, MemoryProtection::NoAccess);
                        *protection = ProtectionState::NoAccess;
                    }
                }
            }
        }
    }
}
```

### Cleanup Process

```rust
fn cleanup_memory(&self) -> Result<()> {
    // Check if already freed
    if self.inner.freed.compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire).is_err() {
        return Ok(()); // Already freed
    }
    
    // Capture local copies to avoid races
    let ptr = self.inner.ptr;
    let capacity = self.inner.capacity;
    
    // Enable write access for cleanup
    {
        let mut protection = self.inner.protection.lock()?;
        
        // Re-check pointer validity after acquiring lock
        if ptr.is_null() {
            return Ok(());
        }
        
        let full_slice = unsafe { std::slice::from_raw_parts_mut(ptr, capacity) };
        memcall::protect(full_slice, MemoryProtection::ReadWrite)?;
        *protection = ProtectionState::ReadWrite;
    }
    
    // Unlock memory
    #[cfg(not(feature = "no-mlock"))]
    if self.inner.len > 0 {
        let slice = unsafe { std::slice::from_raw_parts_mut(ptr, self.inner.len) };
        let _ = memcall::unlock(slice);
    }
    
    // Wipe memory
    unsafe {
        std::ptr::write_bytes(ptr, 0, capacity);
    }
    
    // Free the memory
    unsafe {
        let _ = memcall::free_aligned(ptr, capacity);
    }
    
    Ok(())
}
```

## Error Handling

1. **Recoverable Errors**:
   - `SecureMemoryError::AllocationFailed`: Memory allocation issues
   - `SecureMemoryError::ProtectionFailed`: Memory protection operations failed
   - `SecureMemoryError::SecretClosed`: Attempted to access closed secret

2. **Consistent Error Propagation**:
   - Use `Result<T, SecureMemoryError>` throughout the API
   - Convert system errors to appropriate `SecureMemoryError` variants
   - Provide detailed error messages where appropriate

## Testing Strategy

1. **Unit Tests**:
   - Testing basic functionality
   - Verifying proper error handling
   - Ensuring state transitions work correctly

2. **Concurrency Tests**:
   - High concurrency tests with multiple threads
   - Race condition tests for close/access races
   - Stress tests for reliability under load

3. **Memory Safety Tests**:
   - Verifying protection transitions work
   - Testing use-after-free scenarios
   - Signal handling tests (SIGBUS/SIGSEGV)

4. **Performance Tests**:
   - Benchmarks for key operations
   - Regression testing against previous versions
   - Optimized vs. non-optimized builds

## Platform Considerations

1. **Cross-Platform Memory Operations**:
   - Handle platform-specific memory alignment
   - Use appropriate system calls for each platform
   - Fall back gracefully on unsupported platforms

2. **Platform-Specific Optimizations**:
   - Use platform-specific features where available
   - Optimize for common use cases on each platform
   - Consistent API across all supported platforms

## Future Improvements

1. **Memory Abstraction**:
   - Create dedicated `AlignedMemory` type
   - Encapsulate allocation and protection
   - Provide consistent RAII semantics

2. **Enhanced State Machine**:
   - Formalize valid state transitions
   - Add transition validation
   - Integrate with memory management

3. **Unified Implementation**:
   - Combine best aspects of existing implementations
   - Simplify API surface
   - Maintain backward compatibility
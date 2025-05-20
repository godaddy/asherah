# Memguard Lock Ordering Strategy

This document outlines the established lock ordering for global state in memguard. Following this order consistently will prevent deadlocks in the shared state.

## Global Locks

The memguard library uses two main global locks:

1. `COFFER` - Manages the global encryption key 
2. `BUFFERS` - Tracks active secure buffers (BufferRegistry)

## Lock Ordering Rules

The following lock ordering **MUST** be maintained throughout the codebase:

1. `COFFER` must always be acquired **before** `BUFFERS`
2. Never hold a `Buffer.inner` lock while attempting to acquire a global lock

### Example Flow:

```rust
// CORRECT ORDERING
let coffer = globals::get_coffer().lock().unwrap();
// ... perform operations with coffer ...
let buffers = globals::get_buffer_registry().lock().unwrap();
// ... perform operations with buffers ...
```

```rust
// INCORRECT - Never do this!
let buffers = globals::get_buffer_registry().lock().unwrap();
// ...
let coffer = globals::get_coffer().lock().unwrap(); // Potential deadlock!
```

## Buffer and Registry Interaction

The most common deadlock scenario occurs when:
1. Thread A acquires the `BUFFERS` registry lock to track/modify buffers
2. Thread B locks a specific `Buffer.inner` and attempts to acquire the `BUFFERS` registry lock
3. If Thread A is then waiting for the `Buffer.inner` lock, a deadlock occurs

### Prevention Strategy:

1. Use `try_lock()` instead of `lock()` when acquiring a second lock
2. If `try_lock()` fails, release all held locks and retry with proper ordering
3. In critical operations (like `destroy()`), set flags to indicate operation in progress before acquiring locks

## Implementation Guidelines

1. **Registry Operations**:
   - When adding/removing from the registry, acquire the registry lock first
   - Do not hold buffer locks when modifying the registry
   - Use weak references in the registry to avoid circular references

2. **Buffer Destruction**:
   - Mark buffers as destroyed with an atomic flag before acquiring locks
   - Use `try_lock()` for buffer state to avoid blocking indefinitely
   - Remove from registry after updating internal state, not before

3. **Coffer Operations**:
   - The coffer rekey thread should only acquire the coffer lock
   - If buffer operations are needed, release the coffer lock first

4. **Deadlock Recovery**:
   - All lock acquisitions should have timeouts or use `try_lock()`
   - Implement fallback mechanisms when locks cannot be acquired

## Advanced Considerations

### Test Mode Special Handling:

In test mode (`#[cfg(test)]`), we use a different strategy to avoid deadlocks:
1. Skip registry operations entirely
2. Use direct buffer management to avoid lock contention
3. Make canary verification optional

### Global State Initialization:

The initialization sequence matters:
1. Initialize `COFFER` first, then `BUFFERS`
2. Use `OnceLock::get_or_init` to ensure thread-safe lazy initialization

## Code Paths Requiring Special Attention

1. `Buffer::destroy()` - Interacts with registry
2. `Buffer::new()` - Adds buffer to registry
3. `BufferRegistry::destroy_all()` - Tries to lock multiple buffers
4. `Coffer::rekey()` - Called by background thread, holds global coffer lock
5. `Enclave::seal()` - Uses both coffer and buffer operations

By following these guidelines, we will prevent deadlocks in the shared state management of memguard.
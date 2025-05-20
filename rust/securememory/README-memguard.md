# Memguard Module for Rust Securememory

This module provides a Rust implementation of the [Go Memguard](https://github.com/awnumar/memguard) library for the Asherah project's securememory crate. Memguard offers enhanced security features for handling sensitive data in memory, beyond the basic protections provided by the standard securememory implementation.

## Features

- **Guard Pages**: Protects sensitive data with inaccessible memory pages on either side to detect and prevent buffer overflows
- **Canary Values**: Continuously verifies memory integrity to detect memory corruption
- **Mutability Control**: Fine-grained control over memory mutability with freeze/melt operations
- **Enclave System**: Encrypted secure storage for data at rest in memory
- **Secure Key Management**: Auto-rekeying key manager that stores encryption keys securely
- **Global Registry**: Tracks all secure buffers for collective operations and emergency wiping
- **Emergency Handling**: Mechanisms for rapid secure cleanup in security incidents

## Usage

### Basic Buffer Operations

```rust
use securememory::memguard::Buffer;

// Create a secure buffer
let mut buffer = Buffer::new(32).unwrap();

// Write data to the buffer
buffer.with_data_mut(|data| {
    // Fill with some sensitive data
    for (i, byte) in data.iter_mut().enumerate() {
        *byte = (i % 256) as u8;
    }
    Ok(())
}).unwrap();

// Freeze the buffer to make it immutable
buffer.freeze().unwrap();

// Read the data
buffer.with_data(|data| {
    println!("First byte: {}", data[0]);
    Ok(())
}).unwrap();

// Make buffer mutable again
buffer.melt().unwrap();

// Fill with secure random data
buffer.scramble().unwrap();

// Destroy the buffer when done (also happens on drop)
buffer.destroy().unwrap();
```

### Enclave for Encrypted Storage

```rust
use securememory::memguard::{Buffer, Enclave};

// Create a buffer with sensitive data
let mut buffer = Buffer::new(32).unwrap();
buffer.with_data_mut(|data| {
    // Fill with data
    for i in 0..data.len() {
        data[i] = i as u8;
    }
    Ok(())
}).unwrap();

// Seal buffer into an enclave (buffer is destroyed)
let enclave = Enclave::seal(&mut buffer).unwrap();
assert!(!buffer.alive());

// Later, open the enclave to retrieve the data
let unsealed = enclave.open().unwrap();
unsealed.with_data(|data| {
    println!("Retrieved data from enclave");
    Ok(())
}).unwrap();
```

### Emergency Operations

```rust
use securememory::memguard::{purge, safe_exit, safe_panic};

// In case of security incident, wipe all sensitive data immediately
purge();

// For controlled shutdown with cleanup
safe_exit(0);

// For panic with memory wiping
if security_breach_detected {
    safe_panic("Security breach detected");
}
```

### Utility Functions

```rust
use securememory::memguard::{scramble_bytes, wipe_bytes};

// Generate random data
let mut key = vec![0u8; 32];
scramble_bytes(&mut key);

// Use the key...

// Wipe sensitive data
wipe_bytes(&mut key);
```

## Implementation Details

The Rust implementation follows the same architecture as the Go version, but leverages Rust's safety features and ownership model:

- **Memory Safety**: Uses Rust's ownership and borrowing rules for safer memory management
- **No Global State**: Uses thread-safe globals with `OnceLock` instead of package-level variables
- **Error Handling**: Comprehensive error handling with the `Result` type
- **Thread Safety**: All components are thread-safe and can be shared across threads

## Security Considerations

- **Guard Pages**: Buffer data is surrounded by inaccessible memory pages to detect buffer overflows
- **Canary Verification**: Detects memory tampering via random canary values
- **Automatic Wiping**: Memory is zeroed before deallocation
- **Memory Locking**: Prevents sensitive data from being swapped to disk
- **Prevention of Use-After-Free**: Robust tracking of buffer state prevents use after destruction
- **Timing Attack Resistance**: Uses constant-time operations for sensitive comparisons

## Advanced Features

### Coffer Key Manager

The Coffer provides a secure container for cryptographic keys:

```rust
use securememory::memguard::Coffer;

// Create a new key manager
let coffer = Coffer::new();

// Get a view of the key for use
let key_view = coffer.view().unwrap();

// Use the key for encryption
key_view.with_data(|key| {
    // Use the key...
    Ok(())
}).unwrap();

// key_view is automatically wiped when dropped
```

The Coffer:
- Stores the key split across multiple secure buffers
- Automatically re-keys itself at regular intervals
- Never exposes the raw key material directly
- Can be explicitly destroyed when no longer needed

## Running the Example

```bash
cargo run --example memguard_example
```

This will demonstrate the core functionality of the memguard module, including buffer creation, data protection, encryption with enclaves, and emergency data wiping.
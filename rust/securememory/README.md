# SecureMemory

A Rust library for securely handling sensitive data in memory with enhanced security features.

## Overview

SecureMemory provides a secure way to handle sensitive data (e.g., cryptographic keys, passwords) in memory. It includes features to protect memory from unauthorized access, prevent swapping to disk, and securely erase memory when it's no longer needed.

## Features

- **Memory Protection**: Dynamically controls memory access (no access, read-only, read-write)
- **Memory Locking**: Prevents sensitive data from being swapped to disk
- **Secure Zeroing**: Ensures all memory is wiped before deallocation
- **Thread Safety**: All operations are thread-safe and can be safely shared across threads
- **Race Condition Protection**: Robust synchronization prevents deadlocks and race conditions
- **Constant-Time Operations**: Helps prevent timing attacks
- **Cross-Platform**: Supports Linux, macOS, Windows, FreeBSD, NetBSD, OpenBSD, and Solaris

## Installation

Add SecureMemory to your `Cargo.toml`:

```toml
[dependencies]
securememory = { git = "https://github.com/godaddy/asherah.git" }
```

## Basic Usage

```rust
use securememory::secret::{Secret, SecretFactory};
use securememory::protected_memory::DefaultSecretFactory;

// Create a new secret factory
let factory = DefaultSecretFactory::new();

// Create a secret with sensitive data
let mut data = b"my-secret-key".to_vec();
let secret = factory.new(&mut data).unwrap();
// At this point, data has been wiped

// Use the secret
secret.with_bytes(|bytes| {
    // Do something with the bytes here
    println!("Secret length: {}", bytes.len());
    Ok(())
}).unwrap();

// Secret will be securely wiped when dropped
drop(secret);
```

## Advanced Usage

### Creating Random Secrets

```rust
use securememory::secret::{Secret, SecretFactory};
use securememory::protected_memory::DefaultSecretFactory;

// Create a new secret factory
let factory = DefaultSecretFactory::new();

// Create a secret with random data
let secret = factory.create_random(32).unwrap(); // 32 bytes

// Use the secret
secret.with_bytes(|bytes| {
    println!("Random data: {:?}", bytes);
    Ok(())
}).unwrap();
```

### Using with Reader Interface

```rust
use securememory::secret::{Secret, SecretFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::io::Read;

// Create a new secret factory
let factory = DefaultSecretFactory::new();

// Create a secret
let mut data = b"my-secret-key".to_vec();
let secret = factory.new(&mut data).unwrap();

// Use the secret through the Reader interface
let mut reader = secret.reader().unwrap();
let mut buffer = vec![0u8; 13]; // Size of "my-secret-key"
reader.read_exact(&mut buffer).unwrap();

assert_eq!(buffer, b"my-secret-key");
```

### Concurrent Access Safety

SecureMemory is designed to be safely used across multiple threads:

```rust
use securememory::secret::{Secret, SecretFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::thread;

// Create a new secret factory
let factory = DefaultSecretFactory::new();

// Create a secret
let mut data = b"my-secret-key".to_vec();
let secret = Arc::new(factory.new(&mut data).unwrap());

// Create multiple threads that access the secret
let mut handles = vec![];
for _ in 0..10 {
    let secret_clone = Arc::clone(&secret);
    let handle = thread::spawn(move || {
        secret_clone.with_bytes(|bytes| {
            // Do something with the bytes here
            println!("Secret length: {}", bytes.len());
            Ok(())
        }).unwrap();
    });
    handles.push(handle);
}

// Wait for all threads to complete
for handle in handles {
    handle.join().unwrap();
}
```

## API Documentation

For full API documentation, use `cargo doc --open` to generate and view the documentation.

Key components:

- `Secret`: A trait for handling sensitive data in memory
- `SecretFactory`: A trait for creating Secret instances
- `DefaultSecretFactory`: Default implementation of SecretFactory that uses protected memory
- `MemoryManager`: Low-level memory management operations

## Platform Support

- Linux: Full support
- macOS: Full support 
- Windows: Support with the `windows` feature enabled
- FreeBSD: Full support
- NetBSD: Full support
- OpenBSD: Full support
- Solaris: Full support

For detailed information about platform-specific implementations, see [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

## License

Apache License, Version 2.0
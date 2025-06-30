# Stream API for SecureMemory

The Stream API for SecureMemory provides a way to handle large amounts of sensitive data in memory securely. It implements the standard Rust `Read` and `Write` traits, making it compatible with the rest of the Rust I/O ecosystem while ensuring that sensitive data remains protected.

## Key Features

- **Memory Protection**: All data stored in the stream is encrypted and protected when not in use
- **Chunked Storage**: Large data is broken down into manageable chunks to limit the amount of data exposed at once
- **Standard I/O Interface**: Implements `Read` and `Write` traits for compatibility with Rust's I/O ecosystem
- **Thread Safety**: Can be safely shared between threads
- **Secure Memory Management**: All memory is properly wiped and protected
- **Zero-Copy Operation**: Minimizes copying of sensitive data where possible

## When to Use Stream API

The Stream API is particularly useful in the following scenarios:

1. When you need to work with large amounts of sensitive data that may not fit entirely in memory
2. When you need to process sensitive data in chunks
3. When you want to interface with existing code that expects `Read` or `Write` implementations
4. When you need to combine multiple sensitive data sources into a single operation

## Basic Usage

### Creating a Stream

```rust
use securememory::protected_memory::DefaultSecretFactory;
use securememory::stream::Stream;

// Create a new stream with the default secret factory
let factory = DefaultSecretFactory::new();
let stream = Stream::new(factory);
```

### Writing Data to a Stream

```rust
use std::io::Write;

// Write sensitive data to the stream
let sensitive_data = b"This is sensitive information that should be protected";
stream.write_all(sensitive_data).unwrap();
```

### Reading Data from a Stream

```rust
use std::io::Read;

// Read data in chunks
let mut buffer = [0u8; 16];
while let Ok(bytes_read) = stream.read(&mut buffer) {
    if bytes_read == 0 {
        break; // End of stream
    }
    
    // Process the chunk securely
    // ...
}
```

### Getting the Stream Size

```rust
// Get the total amount of data in the stream
let size = stream.size().unwrap();
println!("Stream contains {} bytes of protected data", size);
```

### Using Next and Flush

```rust
// Get the next chunk as a Secret
let secret = stream.next().unwrap();

// Use the secret
secret.with_bytes(|bytes| {
    // Process the bytes
    println!("Processing {} bytes", bytes.len());
    Ok(())
}).unwrap();

// Or get all remaining data as a single Secret
let all_data = stream.flush().unwrap();
all_data.with_bytes(|bytes| {
    // Process all bytes
    println!("Got all {} bytes", bytes.len());
    Ok(())
}).unwrap();
```

## Advanced Usage

### Processing Files Securely

```rust
use std::fs::File;
use std::io::{Read, Write};

// Read a sensitive file into a protected stream
let mut file = File::open("sensitive_data.txt").unwrap();
let factory = DefaultSecretFactory::new();
let mut stream = Stream::new(factory);

// Copy file contents to secure stream
std::io::copy(&mut file, &mut stream).unwrap();

// Process the data securely
let mut buffer = [0u8; 4096];
while let Ok(bytes_read) = stream.read(&mut buffer) {
    if bytes_read == 0 {
        break;
    }
    
    // Process each chunk securely
    // ...
}
```

### Sharing Between Threads

```rust
use std::sync::Arc;
use std::thread;

let factory = DefaultSecretFactory::new();
let stream = Arc::new(Stream::new(factory));

// Spawn worker threads
let mut handles = Vec::new();
for i in 0..10 {
    let thread_stream = stream.clone();
    let handle = thread::spawn(move || {
        // Each thread can read and write to the stream
        let mut local_stream = thread_stream.clone();
        
        // Write thread-specific data
        let data = format!("Data from thread {}", i).into_bytes();
        local_stream.write_all(&data).unwrap();
        
        // Read data (possibly written by other threads)
        let mut buffer = [0u8; 64];
        while let Ok(n) = local_stream.read(&mut buffer) {
            if n == 0 {
                break;
            }
            // Process data...
        }
    });
    handles.push(handle);
}

// Wait for all threads to complete
for handle in handles {
    handle.join().unwrap();
}
```

## Memory Considerations

The Stream API uses a chunk-based approach to manage memory efficiently. By default, each chunk is 4 times the system's page size, which means the maximum amount of sensitive data that will be in an accessible state at any one time is limited to this chunk size.

If you encounter memory allocation errors, you may need to increase your system's mlock limits. On Unix systems, use `ulimit -l` to check your current limits.

## Performance Tips

1. When reading, try to use buffer sizes that match your expected data usage patterns
2. For optimal performance, prefer reading in multiples of the system page size
3. When writing very large data sets, consider breaking them into reasonably sized chunks before writing
4. Use the `flush()` method to consolidate multiple small chunks into a single Secret when appropriate

## Example

See the `examples/stream.rs` file for a complete working example of the Stream API.
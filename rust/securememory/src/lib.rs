//! # Secure Memory
//!
//! A library for handling sensitive data in memory with enhanced security.
//!
//! The `securememory` library is designed to handle sensitive data (e.g., cryptographic keys)
//! in memory with enhanced security. Its primary functions include allocating memory that
//! can be protected (e.g., made non-readable/writable when not in use via OS-level mechanisms
//! like `mprotect`), locked into physical RAM to prevent swapping (`mlock`), and securely
//! zeroed upon deallocation.
//!
//! ## Features
//!
//! - **Secure Memory Allocation**: Allocates memory that can be protected from unauthorized access
//! - **Memory Protection**: Dynamically changes memory protection between readable and non-accessible
//! - **RAM Locking**: Prevents sensitive data from being swapped to disk
//! - **Secure Zeroing**: Ensures all memory is wiped before deallocation
//! - **Cross-Platform**: Works on Linux, macOS, and Windows
//! - **Thread-Safe**: All operations are thread-safe and can be shared across threads
//! - **Constant-Time Operations**: Helps prevent timing attacks
//! - **Stream Interface**: Process large amounts of sensitive data using standard I/O interfaces
//! - **Advanced Security**: Enhanced memory protection with guard pages and canary values (via memguard module)
//! - **Encrypted Enclaves**: Sealed containers for at-rest memory protection (via memguard module)
//!
//! ## Basic Usage
//!
//! ```rust,no_run
//! use securememory::secret::{Secret, SecretFactory, SecretExtensions};
//! use securememory::protected_memory::DefaultSecretFactory;
//!
//! // Create a new secret factory
//! let factory = DefaultSecretFactory::new();
//!
//! // Create a secret with sensitive data
//! let mut data = b"my-secret-key".to_vec();
//! let mut secret = factory.new(&mut data).unwrap();
//! // At this point, data has been wiped
//!
//! // Use the secret
//! secret.with_bytes(|bytes| {
//!     // Do something with the bytes here
//!     println!("Secret length: {}", bytes.len());
//!     Ok(())
//! }).unwrap();
//!
//! // Secret will be securely wiped when dropped
//! drop(secret);
//! ```
//!
//! ## Advanced Usage
//!
//! ### Creating Random Secrets
//!
//! ```rust,no_run
//! use securememory::secret::{Secret, SecretFactory};
//! use securememory::protected_memory::DefaultSecretFactory;
//! use std::io::Read;
//!
//! // Create a new secret factory
//! let factory = DefaultSecretFactory::new();
//!
//! // Create a secret with random data of the specified size
//! let secret = factory.create_random(32).unwrap();
//!
//! // Use the secret with a reader interface
//! let mut reader = secret.reader().unwrap();
//! let mut buffer = vec![0u8; 32];
//! reader.read_exact(&mut buffer).unwrap();
//! ```
//!
//! ### Using with_bytes_func for Transformations
//!
//! ```rust,no_run
//! use securememory::secret::{Secret, SecretFactory, SecretExtensions};
//! use securememory::protected_memory::DefaultSecretFactory;
//!
//! // Create a new secret factory
//! let factory = DefaultSecretFactory::new();
//!
//! // Create a secret with sensitive data
//! let mut data = b"my-secret-key".to_vec();
//! let mut secret = factory.new(&mut data).unwrap();
//!
//! // Transform the secret and return a result
//! let result = secret.with_bytes_func(|bytes| {
//!     // For example, convert the bytes to a hash or use them in a cryptographic operation
//!     let transformed_data = bytes.to_vec(); // Just a copy for this example
//!     Ok(("Operation completed", transformed_data))
//! }).unwrap();
//!
//! assert_eq!(result, "Operation completed");
//! ```
//!
//! ### Using the Stream API for Large Data
//!
//! ```rust,no_run
//! use securememory::protected_memory::DefaultSecretFactory;
//! use securememory::stream::Stream;
//! use std::io::{Read, Write};
//!
//! // Create a stream for handling large amounts of sensitive data
//! let factory = DefaultSecretFactory::new();
//! let mut stream = Stream::new(factory);
//!
//! // Write sensitive data to the stream
//! let sensitive_data = b"This is sensitive information that should be protected";
//! stream.write_all(sensitive_data).unwrap();
//!
//! // Process the data in chunks
//! let mut buffer = [0u8; 16];
//! while let Ok(bytes_read) = stream.read(&mut buffer) {
//!     if bytes_read == 0 {
//!         break; // End of stream
//!     }
//!
//!     // Process the chunk securely
//!     println!("Processing {} bytes", bytes_read);
//!
//!     // The data in buffer is automatically wiped when it goes out of scope
//! }
//! ```
//!
//! ### Using Memguard for Enhanced Security
//!
//! ```rust,no_run
//! use securememory::memguard::{Buffer, Enclave};
//!
//! // Create a secure buffer with guard pages and canary protection
//! let mut buffer = Buffer::new(32).unwrap();
//!
//! // Write data to the buffer
//! buffer.with_data_mut(|data| {
//!     for i in 0..data.len() {
//!         data[i] = i as u8;
//!     }
//!     Ok(())
//! }).unwrap();
//!
//! // Seal the buffer in an encrypted enclave
//! let enclave = Enclave::seal(&mut buffer).unwrap();
//!
//! // Later, retrieve the data by opening the enclave
//! let unsealed = enclave.open().unwrap();
//! unsealed.with_data(|data| {
//!     println!("Retrieved {} bytes from enclave", data.len());
//!     Ok(())
//! }).unwrap();
//! ```
//!
//! ## Error Handling
//!
//! All operations that can fail return a `Result<T, SecureMemoryError>` where `SecureMemoryError`
//! provides detailed information about what went wrong.
//!
//! ```rust,no_run
//! use securememory::secret::{Secret, SecretFactory};
//! use securememory::protected_memory::DefaultSecretFactory;
//! use securememory::Result;
//!
//! fn handle_secret() -> Result<()> {
//!     let factory = DefaultSecretFactory::new();
//!
//!     // This will fail with SecureMemoryError::OperationFailed
//!     let result = factory.create_random(0);
//!
//!     match result {
//!         Ok(_) => println!("Secret created successfully"),
//!         Err(e) => println!("Failed to create secret: {}", e),
//!     }
//!
//!     Ok(())
//! }
//! ```

/// Main secret trait and utilities
pub mod secret;

/// Protected memory implementation of Secret
pub mod protected_memory;

/// Error types
pub mod error;

/// Stream API for handling large amounts of sensitive data
pub mod stream;

/// Advanced memory guarding with enclaves and canary protection
pub mod memguard;

/// Signal handling for secure termination
pub mod signal;

/// Utilities for testing
pub mod test_utils;

// Re-export key types
pub use crate::error::{Result, SecureMemoryError};
pub use crate::protected_memory::DefaultSecretFactory;
pub use crate::secret::{Secret, SecretFactory};

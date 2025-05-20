//! Protected memory implementation of Secret
//!
//! This module provides a concrete implementation of the Secret trait that uses
//! protected memory to store sensitive data securely. The implementation uses
//! OS-specific memory protection facilities to control access to the sensitive data.
//!
//! ## Features
//!
//! - **Dynamic Memory Protection**: Memory containing sensitive data is protected
//!   from access (read/write) when not in use
//! - **Memory Locking**: Prevents sensitive data from being swapped to disk
//! - **Thread Safety**: All secrets can be safely shared across threads
//! - **Secure Destruction**: Memory is securely wiped when no longer needed
//! - **Safe Access Pattern**: Exposes data only through controlled access functions
//!
//! ## Components
//!
//! - **DefaultSecretFactory**: Creates ProtectedMemorySecret instances
//! - **ProtectedMemorySecret**: Concrete implementation of the Secret trait
//!
//! ## Usage
//!
//! The main entry point to this module is the `DefaultSecretFactory`, which implements
//! the `SecretFactory` trait to create new secret instances:
//!
//! ```rust,no_run
//! use securememory::protected_memory::DefaultSecretFactory;
//! use securememory::secret::{Secret, SecretFactory, SecretExtensions};
//!
//! // Create a factory
//! let factory = DefaultSecretFactory::new();
//!
//! // Create a secret from sensitive data
//! let mut sensitive_data = b"super-secret-key".to_vec();
//! let secret = factory.new(&mut sensitive_data).unwrap();
//!
//! // Access the secret safely
//! secret.with_bytes(|bytes| {
//!     // Perform operations with the bytes here
//!     assert_eq!(bytes, b"super-secret-key");
//!     Ok(())
//! }).unwrap();
//!
//! // Create a random secret (e.g., for a cryptographic key)
//! let random_secret = factory.create_random(32).unwrap(); // 32 bytes
//! ```
//!
//! ## Implementation Details
//!
//! The `ProtectedMemorySecret` implementation:
//!
//! 1. Allocates memory for sensitive data
//! 2. Locks the memory to prevent swapping
//! 3. Protects the memory with `NoAccess` when not in use
//! 4. Temporarily changes protection to `ReadOnly` during controlled access
//! 5. Handles concurrent access from multiple threads safely
//! 6. Securely wipes memory before deallocation
//!
//! The memory protection state transitions are:
//!
//! ```text
//! Initial state: NoAccess
//!      |
//!      | (during with_bytes/with_bytes_func)
//!      v
//!    ReadOnly
//!      |
//!      | (after with_bytes/with_bytes_func)
//!      v
//!    NoAccess
//!      |
//!      | (during close)
//!      v
//!    ReadWrite (for zeroing)
//!      |
//!      v
//!    Freed
//! ```

pub mod factory;
pub mod secret;
pub mod secret_simple;

pub use factory::DefaultSecretFactory;
pub use secret::ProtectedMemorySecret;

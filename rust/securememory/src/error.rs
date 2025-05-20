use thiserror::Error;

/// Errors that can occur in the securememory library.
///
/// This enum represents all possible error conditions that can occur when using
/// the securememory library. Each variant includes a description of what went
/// wrong and, where appropriate, additional context information.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::Result;
/// use securememory::secret::{Secret, SecretFactory};
/// use securememory::protected_memory::DefaultSecretFactory;
///
/// fn process_secret() -> Result<()> {
///     let factory = DefaultSecretFactory::new();
///     
///     // This will result in an OperationFailed error
///     let empty_vec = Vec::<u8>::new();
///     let result = factory.new(&mut empty_vec.clone());
///     
///     if let Err(e) = result {
///         println!("Error creating secret: {}", e);
///         // Handle the error appropriately
///     }
///     
///     Ok(())
/// }
/// ```
#[derive(Error, Debug)]
pub enum SecureMemoryError {
    /// The memory could not be allocated.
    ///
    /// This error occurs when the system fails to allocate memory for a secret.
    /// This might happen due to insufficient memory, or OS-specific allocation
    /// failures.
    #[error("Failed to allocate secure memory: {0}")]
    AllocationFailed(String),

    /// The memory could not be locked into RAM.
    ///
    /// This error occurs when the system fails to lock memory into physical RAM,
    /// which is necessary to prevent sensitive data from being swapped to disk.
    /// Common causes include insufficient permissions or resource limits.
    #[error("Failed to lock memory: {0}")]
    MemoryLockFailed(String),

    /// The memory protection could not be set.
    ///
    /// This error occurs when the system fails to change memory protection
    /// settings, such as making memory read-only or no-access. This might be
    /// due to OS limitations or permission issues.
    #[error("Failed to set memory protection: {0}")]
    ProtectionFailed(String),

    /// The memory could not be unlocked.
    ///
    /// This error occurs when the system fails to unlock memory that was
    /// previously locked into RAM. This is usually called during cleanup.
    #[error("Failed to unlock memory: {0}")]
    MemoryUnlockFailed(String),

    /// The memory could not be freed.
    ///
    /// This error occurs when the system fails to deallocate memory. This is
    /// typically a serious error indicating a potential memory leak.
    #[error("Failed to free memory: {0}")]
    DeallocationFailed(String),

    /// The secret has already been closed.
    ///
    /// This error occurs when attempting to use a secret that has already been
    /// closed or destroyed. Once a secret is closed, it cannot be used again.
    #[error("Secret is already closed")]
    SecretClosed,

    /// An invalid size was specified.
    ///
    /// This error occurs when an invalid size is provided for a secret,
    /// such as zero size or a size that exceeds system limits.
    #[error("Invalid size specified: {0}")]
    InvalidSize(String),

    /// A general OS-level error occurred.
    ///
    /// This error captures operating system errors that don't fit into other
    /// specific categories. The string contains the OS error description.
    #[error("OS error: {0}")]
    OsError(String),

    /// An error occurred when trying to use a secret.
    ///
    /// This is a general error that occurs during operations on secrets, such as
    /// creating a secret with invalid parameters or performing invalid operations.
    #[error("Secret operation failed: {0}")]
    OperationFailed(String),
    
    /// Memory is read-only and cannot be modified.
    ///
    /// This error occurs when attempting to modify memory that has been marked
    /// as read-only, such as trying to write to a frozen buffer.
    #[error("Memory is read-only: {0}")]
    ReadOnlyMemory(String),
    
    /// Failed to generate secure random data.
    ///
    /// This error occurs when the system fails to generate cryptographically
    /// secure random data, which is used for keys, nonces, and other security-critical
    /// values.
    #[error("Random generation failed: {0}")]
    RandomGenerationFailed(String),
    
    /// Memory corruption or buffer overflow detected.
    ///
    /// This error occurs when memory corruption is detected, such as when
    /// a canary value has been modified, indicating a potential buffer overflow
    /// or other memory safety issue.
    #[error("Memory corruption detected: {0}")]
    MemoryCorruption(String),
}

impl From<memguard::MemguardError> for SecureMemoryError {
    fn from(err: memguard::MemguardError) -> Self {
        use memguard::MemguardError;
        match err {
            MemguardError::SecretClosed => SecureMemoryError::SecretClosed,
            MemguardError::ProtectionFailed(msg) => SecureMemoryError::ProtectionFailed(msg),
            MemguardError::MemoryLockFailed(msg) => SecureMemoryError::MemoryLockFailed(msg),
            MemguardError::MemoryUnlockFailed(msg) => SecureMemoryError::MemoryUnlockFailed(msg),
            MemguardError::OperationFailed(msg) => SecureMemoryError::OperationFailed(msg),
            MemguardError::MemcallError(e) => SecureMemoryError::OsError(e.to_string()),
            MemguardError::CryptoError(msg) => SecureMemoryError::OperationFailed(format!("Crypto error: {}", msg)),
            MemguardError::OsError(msg) => SecureMemoryError::OsError(msg),
            MemguardError::IoError(e) => SecureMemoryError::OsError(e.to_string()),
            MemguardError::MemoryCorruption(msg) => SecureMemoryError::MemoryCorruption(msg),
        }
    }
}

/// Result type for securememory operations.
///
/// This type alias is used throughout the library to represent operation results
/// that may fail with a `SecureMemoryError`.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::Result;
/// use securememory::secret::{Secret, SecretFactory, SecretExtensions};
/// use securememory::protected_memory::DefaultSecretFactory;
///
/// fn use_secret() -> Result<String> {
///     let factory = DefaultSecretFactory::new();
///     let mut data = b"sensitive-data".to_vec();
///     let secret = factory.new(&mut data)?;
///     
///     secret.with_bytes_func(|bytes| {
///         let result = String::from_utf8_lossy(bytes).to_string();
///         Ok((result, Vec::new()))
///     })
/// }
/// ```
pub type Result<T> = std::result::Result<T, SecureMemoryError>;
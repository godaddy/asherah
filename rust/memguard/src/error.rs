use thiserror::Error;

/// Defines the set of errors that can occur within the `memguard` library.
#[derive(Error, Debug)]
pub enum MemguardError {
    /// Indicates an attempt to use a resource (like a `Buffer`, `Enclave`, or `Coffer`)
    /// that has already been securely destroyed or "closed".
    ///
    /// This typically occurs if methods are called on an object after its `destroy()`
    /// method (or `Drop` implementation) has been executed, or if a global resource
    /// like the `Coffer` has been purged.
    #[error("Secret is already closed")]
    SecretClosed,

    /// An error occurred while trying to change memory protection flags
    /// (e.g., making memory read-only, read-write, or no-access) using `mprotect` or `VirtualProtect`.
    /// Contains a message describing the specific failure.
    ///
    /// This can happen due to invalid arguments, permission issues, or other system-level problems.
    #[error("Memory protection failed: {0}")]
    ProtectionFailed(String),

    /// An error occurred while trying to lock memory pages into RAM using `mlock`
    /// or `VirtualLock`, to prevent the data from being swapped to disk.
    /// Contains a message describing the failure.
    ///
    /// This might be due to insufficient permissions or exceeding system limits on locked memory.
    #[error("Memory lock failed: {0}")]
    MemoryLockFailed(String),

    /// An error occurred while trying to unlock memory pages that were previously locked
    /// using `munlock` or `VirtualUnlock`. Contains a message describing the failure.
    #[error("Memory unlock failed: {0}")]
    MemoryUnlockFailed(String),

    /// A generic failure for operations not covered by more specific error types.
    /// This can indicate internal logical errors, unexpected states, or failures from
    /// underlying operations (e.g., random number generation, canary checks).
    /// Contains a message describing the specific failure.
    #[error("Operation failed: {0}")]
    OperationFailed(String),

    /// An error originating from the underlying `memcall-rs` library, which
    /// handles low-level memory allocation and protection primitives.
    /// This wraps `memcall::MemcallError`.
    #[error("Memory system error: {0}")]
    MemcallError(#[from] memcall::MemcallError),

    /// An error related to cryptographic operations, such as encryption,
    /// decryption, or authentication (e.g., MAC verification failure in AEAD).
    /// Contains a message describing the specific cryptographic failure.
    ///
    /// This is often returned by `Enclave` operations if decryption fails due to a
    /// wrong key (e.g., after `purge()`) or corrupted ciphertext.
    #[error("Cryptographic operation failed: {0}")]
    CryptoError(String),

    /// An error originating from the operating system that is not directly covered by
    /// `memcall::MemcallError` or `std::io::Error`. This is typically used for
    /// OS-specific issues like signal handler registration failures.
    /// Contains a message describing the failure.
    #[error("OS error: {0}")]
    OsError(String),

    /// An I/O error, typically encountered during stream operations (`Stream::read`, `Stream::write`)
    /// or when creating `Buffer`s from `std::io::Read` sources.
    /// This wraps a `std::io::Error`.
    #[error("I/O error: {0}")]
    IoError(#[from] std::io::Error),
    
    /// Indicates that memory (usually a buffer's canary) has been corrupted,
    /// possibly as a result of a buffer overflow or external tampering.
    #[error("Memory corruption detected: {0}")]
    MemoryCorruption(String),
}

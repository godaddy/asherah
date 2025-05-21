use thiserror::Error;

/// Result type for appencryption operations
pub type Result<T> = std::result::Result<T, Error>;

/// Errors that can occur in the appencryption library
#[derive(Error, Debug)]
pub enum Error {
    /// Errors related to key management service operations
    #[error("KMS error: {0}")]
    Kms(String),

    /// Errors related to metastore operations
    #[error("Metastore error: {0}")]
    Metastore(String),

    /// Errors related to cryptographic operations
    #[error("Crypto error: {0}")]
    Crypto(String),

    /// Errors related to secure memory operations
    #[error("Secure memory error: {0}")]
    SecureMemory(#[from] securememory::SecureMemoryError),

    /// Errors related to JSON serialization/deserialization
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    /// Errors related to I/O operations
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),

    /// Errors related to invalid key state
    #[error("Invalid key state: {0}")]
    InvalidKeyState(String),

    /// Errors related to invalid partition IDs
    #[error("Invalid partition: {0}")]
    InvalidPartition(String),

    /// General internal errors
    #[error("Internal error: {0}")]
    Internal(String),

    /// Key not found error
    #[error("Key not found: {0}")]
    KeyNotFound(String),

    /// Invalid argument error
    #[error("Invalid argument: {0}")]
    InvalidArgument(String),

    /// Feature not implemented error
    #[error("Not implemented: {0}")]
    NotImplemented(String),
}

impl From<Box<dyn std::error::Error + Send + Sync>> for Error {
    fn from(err: Box<dyn std::error::Error + Send + Sync>) -> Self {
        Error::Internal(err.to_string())
    }
}

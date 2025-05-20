use thiserror::Error;

/// Errors that can occur during memory-related operations.
#[derive(Error, Debug)]
pub enum MemcallError {
    /// Operation failed due to a system error.
    #[error("System operation failed: {0}")]
    SystemError(String),
    
    /// Invalid arguments were provided to the operation.
    #[error("Invalid arguments: {0}")]
    InvalidArgument(String),
    
    /// The requested operation is not supported on this platform.
    #[error("Operation not supported on this platform: {0}")]
    NotSupported(String),
    
    /// The system is out of memory or hit a resource limit.
    #[error("Resource limit reached: {0}")]
    ResourceLimit(String),
    
    /// The operation failed due to insufficient permissions.
    #[error("Permission denied: {0}")]
    PermissionDenied(String),
    
    /// Some other error occurred.
    #[error("Operation failed: {0}")]
    OperationFailed(String),
}
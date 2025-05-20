use crate::error::Result;
use std::io::Read;
use std::any::Any;

// Type-erased helper trait that enables safe downcasting
pub trait AnySecret: Any + Send + Sync {
    fn as_any(&self) -> &dyn Any;
    fn as_any_mut(&mut self) -> &mut dyn Any;
}

impl<T: Any + Send + Sync> AnySecret for T {
    fn as_any(&self) -> &dyn Any {
        self
    }
    
    fn as_any_mut(&mut self) -> &mut dyn Any {
        self
    }
}

/// The Secret trait defines the basic operations for a secure secret
pub trait Secret: AnySecret {
    /// Check if the secret has been closed
    fn is_closed(&self) -> bool;
    
    /// Close the secret, wiping its memory
    fn close(&self) -> Result<()>;
    
    /// Get a reader for the secret
    fn reader(&self) -> Result<Box<dyn Read + Send + Sync + '_>>;
    
    /// Get the length of the secret in bytes
    fn len(&self) -> usize;
    
    /// Check if the secret is empty
    fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

/// SecretExtensions provides methods for working with secrets
pub trait SecretExtensions {
    /// Provides temporary, safe, read-only access to the secret byte data.
    /// 
    /// The memory is made accessible only for the duration of the provided closure's
    /// execution. If the closure returns an error, it will be propagated.
    fn with_bytes<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<R>;

    /// Provides temporary, safe, read-only access to the secret byte data, allowing
    /// the closure to return a new byte slice.
    ///
    /// This function is similar to `with_bytes`, but allows the action closure to 
    /// return both a result and a new byte vector. This is useful for transformations
    /// that need to modify the secret data.
    fn with_bytes_func<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<(R, Vec<u8>)>;
}

/// A trait for creating new secrets.
///
/// This trait is implemented by secret factory implementations to provide
/// a consistent interface for creating new secrets from byte slices.
///
/// # Example
///
/// ```rust,no_run
/// # use securememory::secret::{Secret, SecretFactory};
/// # use securememory::protected_memory::DefaultSecretFactory;
/// #
/// // Create a new factory
/// let factory = DefaultSecretFactory::new();
/// 
/// // Create a new secret from a mutable byte slice
/// let mut password = b"secure-password-123".to_vec();
/// let secret = factory.new(&mut password).unwrap();
/// 
/// // Password data is now wiped from the original slice
/// assert_ne!(password, b"secure-password-123");
/// 
/// // Use the secret...
/// 
/// // Explicitly close the secret when done (also happens in Drop)
/// secret.close().unwrap();
/// assert!(secret.is_closed());
/// ```
pub trait SecretFactory: Send + Sync {
    /// The type of secret this factory creates
    type SecretType: Secret + SecretExtensions;
    
    /// Creates a new secret from the given byte slice.
    ///
    /// This method securely copies the data from the slice into protected memory
    /// and then wipes the original slice. After this method returns, the original
    /// slice will no longer contain the sensitive data.
    ///
    /// # Arguments
    ///
    /// * `b` - A mutable byte slice containing the sensitive data
    ///
    /// # Returns
    ///
    /// * `Result<Self::SecretType>` - A new secret containing the data
    ///
    /// # Errors
    ///
    /// * `SecureMemoryError::MemoryAllocationFailed` - If memory allocation failed
    /// * `SecureMemoryError::ProtectionFailed` - If memory protection couldn't be set
    ///
    /// # Example
    ///
    /// ```rust,no_run
    /// # use securememory::secret::{Secret, SecretFactory};
    /// # use securememory::protected_memory::DefaultSecretFactory;
    /// #
    /// let factory = DefaultSecretFactory::new();
    /// let mut password = b"secure-password-123".to_vec();
    /// let secret = factory.new(&mut password).unwrap();
    ///
    /// // Password data is now wiped from the original slice
    /// assert_ne!(password, b"secure-password-123");
    /// ```
    fn new(&self, b: &mut [u8]) -> Result<Self::SecretType>;

    /// Creates a new secret with random data of the specified size.
    ///
    /// This method allocates protected memory and fills it with
    /// cryptographically secure random data.
    ///
    /// # Arguments
    ///
    /// * `size` - The size in bytes of the random data to generate
    ///
    /// # Returns
    ///
    /// * `Result<Self::SecretType>` - A new secret containing random data
    ///
    /// # Errors
    ///
    /// * `SecureMemoryError::MemoryAllocationFailed` - If memory allocation failed
    /// * `SecureMemoryError::ProtectionFailed` - If memory protection couldn't be set
    /// * `SecureMemoryError::RandomGenerationFailed` - If random data generation failed
    ///
    /// # Example
    ///
    /// ```rust,no_run
    /// # use securememory::secret::{Secret, SecretFactory};
    /// # use securememory::protected_memory::DefaultSecretFactory;
    /// #
    /// let factory = DefaultSecretFactory::new();
    /// let secret = factory.create_random(32).unwrap(); // 32-byte random secret
    /// ```
    fn create_random(&self, size: usize) -> Result<Self::SecretType>;
}

/// A secure reader implementation that provides read access to a secret.
///
/// This struct implements the `Read` trait, allowing secrets to be used
/// with standard I/O interfaces.
pub struct SecretReader<'a, S: Secret + SecretExtensions + ?Sized> {
    /// Reference to the secret being read
    secret: &'a S,
    /// Current position within the secret
    position: usize,
}

impl<'a, S: Secret + SecretExtensions + ?Sized> SecretReader<'a, S> {
    /// Creates a new SecretReader for the given secret.
    ///
    /// # Arguments
    ///
    /// * `secret` - The secret to read from
    ///
    /// # Returns
    ///
    /// * `SecretReader` - A new SecretReader instance
    pub fn new(secret: &'a S) -> Self {
        Self {
            secret,
            position: 0,
        }
    }
}

impl<'a, S: Secret + SecretExtensions + ?Sized> Read for SecretReader<'a, S> {
    fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
        let result = self.secret.with_bytes(|bytes| {
            // Check for EOF
            if self.position >= bytes.len() {
                return Ok(0);
            }
            
            let remaining = bytes.len() - self.position;
            let to_read = std::cmp::min(remaining, buf.len());
            
            buf[..to_read].copy_from_slice(&bytes[self.position..self.position + to_read]);
            self.position += to_read;
            
            Ok(to_read)
        });

        match result {
            Ok(bytes_read) => Ok(bytes_read),
            Err(e) => Err(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("Secret read error: {:?}", e),
            )),
        }
    }
}
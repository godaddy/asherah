use crate::error::{Result, SecureMemoryError};
use crate::protected_memory::secret::ProtectedMemorySecret;
use crate::secret::SecretFactory;
use std::time::Instant;
use subtle::ConstantTimeEq;
use zeroize::Zeroize;

/// Default implementation of SecretFactory that produces ProtectedMemorySecret instances.
///
/// This factory uses the platform's memory protection features to create
/// secrets that are securely stored in memory, protected from observation
/// and tampering.
///
/// # Example
///
/// ```rust,no_run
/// # use securememory::secret::SecretFactory;
/// # use securememory::protected_memory::DefaultSecretFactory;
/// #
/// // Create a factory
/// let factory = DefaultSecretFactory::new();
///
/// // Create a secret from a password
/// let mut password = b"secure-password-123".to_vec();
/// let secret = factory.new(&mut password).unwrap();
///
/// // Password data is now wiped from the original slice
/// assert_ne!(password, b"secure-password-123");
/// ```
#[derive(Clone)]
pub struct DefaultSecretFactory;

impl DefaultSecretFactory {
    /// Creates a new DefaultSecretFactory.
    ///
    /// # Returns
    ///
    /// A new DefaultSecretFactory instance
    ///
    /// # Example
    ///
    /// ```rust,no_run
    /// # use securememory::protected_memory::DefaultSecretFactory;
    /// #
    /// let factory = DefaultSecretFactory::new();
    /// ```
    pub fn new() -> Self {
        Self
    }
}

impl Default for DefaultSecretFactory {
    fn default() -> Self {
        Self::new()
    }
}

impl SecretFactory for DefaultSecretFactory {
    type SecretType = ProtectedMemorySecret;
    
    /// Creates a new Secret from the provided byte array.
    ///
    /// This method securely copies the data from the input array to a new
    /// Secret instance. The input array is zeroed out to protect the original
    /// sensitive data. The copying is done in constant time to avoid timing
    /// attacks.
    ///
    /// # Arguments
    ///
    /// * `b` - A mutable byte slice containing the data to store in the Secret
    ///
    /// # Returns
    ///
    /// A new Secret instance containing the data
    ///
    /// # Errors
    ///
    /// Returns an error if memory allocation or protection fails, or if the
    /// input array is empty.
    ///
    /// # Example
    ///
    /// ```rust,no_run
    /// # use securememory::secret::SecretFactory;
    /// # use securememory::protected_memory::DefaultSecretFactory;
    /// #
    /// let factory = DefaultSecretFactory::new();
    ///
    /// // Create a secret from a password
    /// let mut password = b"secure-password-123".to_vec();
    /// let secret = factory.new(&mut password).unwrap();
    ///
    /// // Password data is now wiped from the original slice
    /// assert_ne!(password, b"secure-password-123");
    /// ```
    fn new(&self, b: &mut [u8]) -> Result<Self::SecretType> {
        #[cfg(feature = "metrics")]
        let start = Instant::now();
        #[cfg(not(feature = "metrics"))]
        let _start = Instant::now(); // Used for debugging, prefixed with _ to avoid warning

        if b.is_empty() {
            return Err(SecureMemoryError::OperationFailed(
                "Cannot create a secret from an empty byte slice".to_string(),
            ));
        }

        // Allocate memory for the secret
        let mut bytes = Vec::with_capacity(b.len());
        unsafe {
            // Set the length without initializing the memory
            bytes.set_len(b.len());
        }

        // Perform constant-time copy from input to our memory
        if bytes.ct_eq(b).into() {
            // If the buffers are already equal, no need to copy
            // (this is just a safety check)
        } else {
            // Copy bytes in constant time to avoid timing attacks
            for (dst, src) in bytes.iter_mut().zip(b.iter()) {
                *dst = *src;
            }
        }

        // Wipe the source array
        b.zeroize();

        // Create the protected memory secret
        let secret = ProtectedMemorySecret::new(&bytes)?;

        // Record timing metric
        #[cfg(feature = "metrics")]
        metrics::histogram!("secret.protectedmemory.alloc_duration_seconds").record(start.elapsed().as_secs_f64());

        Ok(secret)
    }

    /// Creates a Secret containing random bytes of the specified size.
    ///
    /// This method allocates a new Secret instance and fills it with
    /// cryptographically secure random bytes.
    ///
    /// # Arguments
    ///
    /// * `size` - The number of random bytes to generate
    ///
    /// # Returns
    ///
    /// A new Secret instance containing random bytes
    ///
    /// # Errors
    ///
    /// Returns an error if memory allocation or protection fails, or if
    /// random number generation fails.
    ///
    /// # Example
    ///
    /// ```rust,no_run
    /// # use securememory::secret::SecretFactory;
    /// # use securememory::protected_memory::DefaultSecretFactory;
    /// #
    /// let factory = DefaultSecretFactory::new();
    ///
    /// // Create a random secret with 32 bytes (e.g., for an AES-256 key)
    /// let secret = factory.create_random(32).unwrap();
    /// ```
    fn create_random(&self, size: usize) -> Result<Self::SecretType> {
        #[cfg(feature = "metrics")]
        let start = Instant::now();
        #[cfg(not(feature = "metrics"))]
        let _start = Instant::now(); // Used for debugging, prefixed with _ to avoid warning

        if size == 0 {
            return Err(SecureMemoryError::OperationFailed(
                "Cannot create a random secret with zero size".to_string(),
            ));
        }

        // Create the protected memory secret with random data
        let secret = ProtectedMemorySecret::from_random(size)?;

        // Record timing metric
        #[cfg(feature = "metrics")]
        metrics::histogram!("secret.protectedmemory.alloc_duration_seconds").record(start.elapsed().as_secs_f64());

        Ok(secret)
    }
}
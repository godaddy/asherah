use crate::error::Result;
use rand::{rngs::OsRng, RngCore};

/// Trait for AEAD (Authenticated Encryption with Associated Data) operations
pub trait AeadImpl: Send + Sync {
    /// Encrypts data using the provided key
    fn encrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>>;

    /// Decrypts data using the provided key
    fn decrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>>;
}

// Constants for GCM mode
const GCM_BLOCK_SIZE: usize = 16; // AES block size
pub(crate) const GCM_NONCE_SIZE: usize = 12;
pub(crate) const GCM_TAG_SIZE: usize = 16;

// Maximum message size supported by GCM
// ((1 << 32) - 2) * GCM_BLOCK_SIZE
pub(crate) const GCM_MAX_DATA_SIZE: usize = ((1 << 32) - 2) * GCM_BLOCK_SIZE;

/// Fills a buffer with random bytes using a cryptographically secure RNG
pub fn fill_random(buffer: &mut [u8]) {
    OsRng.fill_bytes(buffer);
}

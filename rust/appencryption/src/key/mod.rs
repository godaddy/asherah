//! Key management for the application encryption library

pub mod cache;

use crate::error::{Error, Result};
use crate::policy::is_key_expired;
use securememory::{Secret, SecretFactory};
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;
use zeroize::Zeroize;

/// An encrypted cryptographic key stored securely in memory
pub struct CryptoKey {
    /// Timestamp when the key was created
    created: i64,

    /// Secret containing the actual key bytes
    secret: Box<dyn Secret + Send + Sync>,

    /// Flag indicating if the key has been revoked
    revoked: AtomicBool,

    /// Unique ID for this key
    id: String,
}

impl std::fmt::Debug for CryptoKey {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("CryptoKey")
            .field("created", &self.created)
            .field("secret", &"<hidden>")
            .field("revoked", &self.revoked)
            .field("id", &self.id)
            .finish()
    }
}

impl CryptoKey {
    /// Creates a new CryptoKey
    pub fn new(
        id: String,
        created: i64,
        bytes: Vec<u8>,
        secret_factory: &impl SecretFactory,
    ) -> Result<Self> {
        let mut bytes = bytes;
        let secret = secret_factory
            .new(&mut bytes)
            .map_err(Error::SecureMemory)?;

        Ok(Self {
            id,
            created,
            secret: Box::new(secret),
            revoked: AtomicBool::new(false),
        })
    }

    /// Generates a new random key of the specified size
    pub fn generate(
        secret_factory: &impl SecretFactory,
        id: String,
        created: i64,
        size: usize,
    ) -> Result<Self> {
        let secret = secret_factory
            .create_random(size)
            .map_err(Error::SecureMemory)?;

        Ok(Self {
            id,
            created,
            secret: Box::new(secret),
            revoked: AtomicBool::new(false),
        })
    }

    /// Returns the timestamp when the key was created
    pub fn created(&self) -> i64 {
        self.created
    }

    /// Returns the key ID
    pub fn id(&self) -> &str {
        &self.id
    }

    /// Checks if the key has been revoked
    pub fn is_revoked(&self) -> bool {
        self.revoked.load(Ordering::Relaxed)
    }

    /// Marks the key as revoked
    pub fn set_revoked(&self, revoked: bool) {
        self.revoked.store(revoked, Ordering::Relaxed);
    }

    /// Checks if the key is closed
    pub fn is_closed(&self) -> bool {
        self.secret.is_closed()
    }

    /// Securely closes the key
    pub fn close(&mut self) -> Result<()> {
        self.secret.close().map_err(Error::SecureMemory)
    }

    /// Provides temporary access to the key bytes
    pub fn with_bytes<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<R>,
    {
        // Since the trait object doesn't support generic methods, we need to read the bytes
        // via the reader interface and then call the action
        let mut buf = vec![0_u8; self.secret.len()];
        let reader = self.secret.reader().map_err(Error::SecureMemory)?;
        let mut std_reader = std::io::BufReader::new(reader);
        use std::io::Read;
        std_reader.read_exact(&mut buf).map_err(Error::Io)?;
        let result = action(&buf)?;
        buf.zeroize();
        Ok(result)
    }

    /// Provides temporary access to the key bytes, allowing the function to return bytes
    pub fn with_bytes_func<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<(R, Vec<u8>)>,
    {
        // Similar to with_bytes, but we get back a pair and handle the cleanup
        let mut buf = vec![0_u8; self.secret.len()];
        let reader = self.secret.reader().map_err(Error::SecureMemory)?;
        let mut std_reader = std::io::BufReader::new(reader);
        use std::io::Read;
        std_reader.read_exact(&mut buf).map_err(Error::Io)?;

        match action(&buf) {
            Ok((r, mut b)) => {
                b.zeroize();
                buf.zeroize();
                Ok(r)
            }
            Err(e) => {
                buf.zeroize();
                Err(e)
            }
        }
    }
}

/// Checks if a key is invalid (expired or revoked)
pub fn is_key_invalid(key: &CryptoKey, expire_after: Duration) -> bool {
    key.is_revoked() || is_key_expired(key.created(), expire_after)
}

/// Executes a function with access to the key bytes
pub fn with_key_func<F, R>(key: &CryptoKey, action: F) -> Result<R>
where
    F: FnOnce(&[u8]) -> Result<R>,
{
    key.with_bytes(action)
}

/// Securely wipes a byte slice
pub fn mem_clear(bytes: &mut [u8]) {
    bytes.zeroize();
}

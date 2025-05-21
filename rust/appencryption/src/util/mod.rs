//! Utility functions for the application encryption library

use crate::error::Result;
use rand::{rngs::OsRng, RngCore};
use zeroize::Zeroize;

/// Fills a buffer with cryptographically secure random bytes
pub fn fill_random(buf: &mut [u8]) {
    OsRng.fill_bytes(buf);
}

/// Generates a random byte array of the specified size
pub fn get_rand_bytes(size: usize) -> Vec<u8> {
    let mut bytes = vec![0_u8; size];
    fill_random(&mut bytes);
    bytes
}

/// Securely wipes a byte slice
pub fn mem_clear(bytes: &mut [u8]) {
    bytes.zeroize();
}

/// Executes a function that returns bytes, then securely wipes the returned bytes
pub fn with_bytes_func<F, R>(func: F) -> Result<R>
where
    F: FnOnce() -> Result<(R, Vec<u8>)>,
{
    let (result, mut bytes) = func()?;
    mem_clear(&mut bytes);
    Ok(result)
}

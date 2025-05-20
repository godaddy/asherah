//! Cryptographic implementations for the application encryption library

mod aead;
mod aes256gcm;

pub use aead::{AeadImpl, fill_random};
pub use aes256gcm::Aes256GcmAead;
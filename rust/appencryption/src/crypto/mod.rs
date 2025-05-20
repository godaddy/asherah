//! Cryptographic implementations for the application encryption library

mod aead;
pub mod aes256gcm;

pub use aead::{fill_random, AeadImpl};
pub use aes256gcm::Aes256GcmAead;

use crate::error::Result;
use crate::KeyManagementService;
use crate::crypto::{Aes256GcmAead, AeadImpl};
use async_trait::async_trait;

/// A static key management service for testing
///
/// This implementation uses a static master key for encryption/decryption,
/// which is useful for testing but should not be used in production.
pub struct StaticKeyManagementService {
    /// The static master key
    master_key: Vec<u8>,
    /// AEAD implementation for encryption/decryption
    aead: Aes256GcmAead,
}

impl StaticKeyManagementService {
    /// Creates a new StaticKeyManagementService with the given master key
    pub fn new(master_key: Vec<u8>) -> Self {
        Self { 
            master_key,
            aead: Aes256GcmAead::new(),
        }
    }
}

#[async_trait]
impl KeyManagementService for StaticKeyManagementService {
    async fn encrypt_key(&self, key: &[u8]) -> Result<Vec<u8>> {
        // Encrypt the key with the master key using AES-256-GCM
        self.aead.encrypt(key, &self.master_key)
    }
    
    async fn decrypt_key(&self, encrypted_key: &[u8]) -> Result<Vec<u8>> {
        // Decrypt the key with the master key using AES-256-GCM
        self.aead.decrypt(encrypted_key, &self.master_key)
    }
}
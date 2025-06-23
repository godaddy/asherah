use crate::crypto::aead::{fill_random, AeadImpl};
use crate::error::{Error, Result};
use crate::Aead;
use aes_gcm::{
    aead::{Aead as AeadTrait, KeyInit},
    Aes256Gcm, Key as AesKey, Nonce,
};

use super::aead::{GCM_MAX_DATA_SIZE, GCM_NONCE_SIZE, GCM_TAG_SIZE};

/// AES-256-GCM implementation of AEAD
#[derive(Default, Debug, Clone)]
pub struct Aes256GcmAead;

impl Aes256GcmAead {
    /// Creates a new instance of the AES-256-GCM AEAD implementation
    pub fn new() -> Self {
        Self
    }
}

impl AeadImpl for Aes256GcmAead {
    fn encrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        if data.len() > GCM_MAX_DATA_SIZE {
            return Err(Error::Crypto("Data too large for GCM".into()));
        }

        // Convert the key to AES format
        let cipher_key = AesKey::<Aes256Gcm>::from_slice(key);

        // Create the cipher
        let cipher = Aes256Gcm::new(cipher_key);

        // Calculate the output size
        let size = GCM_NONCE_SIZE + data.len() + GCM_TAG_SIZE;

        // Create buffer for encrypted data + nonce
        let mut nonce_and_cipher = vec![0_u8; size];

        // Fill the nonce area with random bytes
        fill_random(&mut nonce_and_cipher[..GCM_NONCE_SIZE]);

        // Create a nonce from the random bytes
        let nonce = Nonce::from_slice(&nonce_and_cipher[..GCM_NONCE_SIZE]);

        // Encrypt the data
        let ciphertext = cipher
            .encrypt(nonce, data)
            .map_err(|e| Error::Crypto(format!("Encryption failed: {}", e)))?;

        // Copy the ciphertext (which includes the tag) after the nonce
        nonce_and_cipher[GCM_NONCE_SIZE..].copy_from_slice(&ciphertext);

        Ok(nonce_and_cipher)
    }

    fn decrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        if data.len() < GCM_NONCE_SIZE + GCM_TAG_SIZE {
            // Must have at least nonce and tag
            return Err(Error::Crypto(
                "Data length is too short for GCM (nonce + tag)".into(),
            ));
        }

        // Convert the key to AES format
        let cipher_key = AesKey::<Aes256Gcm>::from_slice(key);

        // Create the cipher
        let cipher = Aes256Gcm::new(cipher_key);

        // Extract the nonce from the beginning
        let nonce = Nonce::from_slice(&data[..GCM_NONCE_SIZE]);

        // Decrypt the data
        let plaintext = cipher
            .decrypt(nonce, &data[GCM_NONCE_SIZE..]) // Ciphertext + tag follows nonce
            .map_err(|e| Error::Crypto(format!("Decryption failed: {}", e)))?;

        Ok(plaintext)
    }
}

impl Aead for Aes256GcmAead {
    fn encrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        AeadImpl::encrypt(self, data, key)
    }

    fn decrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        AeadImpl::decrypt(self, data, key)
    }
}

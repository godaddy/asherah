use crate::buffer::Buffer;
use crate::error::MemguardError;
use crate::globals;
use crate::util::wipe;
use ring::aead::{self, Aad, LessSafeKey, Nonce, UnboundKey, NONCE_LEN};
use zeroize::Zeroize;

type Result<T> = std::result::Result<T, MemguardError>;

// Constants for all modes
const KEY_SIZE: usize = 32;
const TAG_SIZE: usize = 16;
const NONCE_SIZE: usize = NONCE_LEN;

pub const OVERHEAD: usize = TAG_SIZE + NONCE_SIZE;

fn chacha20_poly1305_alg() -> &'static aead::Algorithm {
    &aead::CHACHA20_POLY1305
}

/// A sealed and encrypted container for sensitive data.
///
/// The `Enclave` provides a way to securely store sensitive data in memory in an encrypted form,
/// using authenticated encryption to protect both confidentiality and integrity. The encryption
/// key is managed by a secure Coffer system that automatically re-keys itself.
///
/// # Security Features
///
/// - **Authenticated Encryption**: Uses ChaCha20-Poly1305 AEAD to protect both confidentiality and integrity
/// - **Secure Key Management**: Encryption keys are managed by the secure Coffer system
/// - **Wipe-on-Use**: The plaintext is wiped after creating an Enclave
///
/// # Examples
///
/// ```rust,no_run
/// use memguard::{Buffer, Enclave};
///
/// // Create a secure buffer with sensitive data
/// let mut buffer = Buffer::new(32).unwrap();
/// buffer.with_data_mut(|data| {
///     // Fill with some sensitive data
///     for i in 0..data.len() {
///         data[i] = i as u8;
///     }
///     Ok(())
/// }).unwrap();
///
/// // Seal the buffer into an encrypted enclave (buffer is destroyed)
/// let enclave = Enclave::seal(&mut buffer).unwrap();
/// assert!(!buffer.is_alive());
///
/// // Later, decrypt the enclave to access the data
/// let unsealed_buffer = enclave.open().unwrap();
/// unsealed_buffer.with_data(|data| {
///     println!("Decrypted data length: {}", data.len());
///     Ok(())
/// }).unwrap();
/// ```
pub struct Enclave {
    // Encrypted data (includes nonce and authentication tag)
    ciphertext: Vec<u8>,
}

impl Enclave {
    /// Creates a new Enclave by encrypting data from a byte slice.
    ///
    /// This function encrypts the provided data and returns it sealed inside an Enclave.
    /// The original data is wiped after encryption.
    ///
    /// # Arguments
    ///
    /// * `data` - Mutable byte slice containing the sensitive data to encrypt
    ///
    /// # Returns
    ///
    /// * `Result<Self>` - A new Enclave containing the encrypted data
    ///
    /// # Errors
    ///
    /// * `MemguardError::OperationFailed` - If data is empty or encryption fails
    /// * `MemguardError::SecretClosed` - If the coffer has been destroyed
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use memguard::Enclave;
    ///
    /// // Encrypt sensitive data
    /// let mut data = vec![1, 2, 3, 4, 5];
    /// let enclave = Enclave::new(&mut data).unwrap();
    ///
    /// // Original data is wiped
    /// assert_ne!(data, vec![1, 2, 3, 4, 5]);
    /// ```
    pub fn new(data: &mut [u8]) -> Result<Self> {
        if data.is_empty() {
            return Err(MemguardError::OperationFailed(
                "Enclave data must not be empty".into(),
            ));
        }

        // Get the encryption key from the coffer
        let coffer = globals::get_coffer();
        let coffer_guard = match coffer.lock() {
            Ok(guard) => guard,
            Err(_) => {
                return Err(MemguardError::OperationFailed(
                    "Failed to lock coffer".into(),
                ))
            }
        };

        let key_buffer = coffer_guard.view()?;

        // Encrypt the data
        let final_ciphertext_result = key_buffer.with_data(|key_bytes| {
            if key_bytes.len() != KEY_SIZE {
                return Err(MemguardError::OperationFailed("Invalid key size".into()));
            }

            // Create the encryption key
            let unbound_key = match UnboundKey::new(chacha20_poly1305_alg(), key_bytes) {
                Ok(key) => key,
                Err(_) => {
                    return Err(MemguardError::OperationFailed(
                        "Failed to create encryption key".into(),
                    ))
                }
            };

            let less_safe_key = LessSafeKey::new(unbound_key);

            // Generate a random nonce
            let mut nonce_bytes = [0u8; NONCE_SIZE];
            match getrandom::getrandom(&mut nonce_bytes) {
                Ok(_) => {}
                Err(e) => {
                    return Err(MemguardError::OperationFailed(format!(
                        "Failed to generate nonce: {}",
                        e
                    )))
                }
            }

            let nonce = Nonce::assume_unique_for_key(nonce_bytes);

            // Allocate space for final ciphertext (nonce + encrypted_data + tag)
            let mut final_ciphertext_vec = Vec::with_capacity(NONCE_SIZE + data.len() + TAG_SIZE);
            final_ciphertext_vec.extend_from_slice(&nonce_bytes); // Prepend nonce

            // Data to be encrypted (make a mutable copy for in-place encryption)
            // Ring's seal_in_place_append_tag appends tag to the input vector.
            let mut data_to_encrypt_and_tag = data.to_vec();

            match less_safe_key.seal_in_place_append_tag(
                nonce,
                Aad::empty(),
                &mut data_to_encrypt_and_tag,
            ) {
                Ok(_) => {}
                Err(_) => return Err(MemguardError::CryptoError("Encryption failed".to_string())),
            }
            final_ciphertext_vec.extend_from_slice(&data_to_encrypt_and_tag); // Append encrypted_data + tag

            Ok(final_ciphertext_vec)
        });

        // Destroy key_buffer regardless of encryption outcome, but after its use.
        if let Err(e) = key_buffer.destroy() {
            log::warn!("Failed to destroy key buffer during Enclave::new: {}", e);
            // If final_ciphertext_result is Ok, this warning is relevant.
            // If final_ciphertext_result is Err, that's the primary error.
        }

        match final_ciphertext_result {
            Ok(encrypted_data) => {
                // Wipe the original data only on successful encryption
                wipe(data);
                Ok(Self {
                    ciphertext: encrypted_data,
                })
            }
            Err(e) => {
                // Plaintext `data` is not wiped if encryption failed.
                Err(e)
            }
        }
    }

    /// Creates a new `Enclave` containing `size` cryptographically-secure random bytes.
    ///
    /// The generated random data is immediately encrypted and sealed within the enclave.
    ///
    /// # Arguments
    ///
    /// * `size` - The number of random bytes to generate and encrypt. Must be greater than 0.
    ///
    /// # Returns
    ///
    /// * `Result<Self>` - A new `Enclave` instance.
    ///
    /// # Errors
    ///
    /// * `MemguardError::OperationFailed` - If `size` is 0, or if random data generation,
    ///   buffer allocation, or encryption fails.
    /// * `MemguardError::SecretClosed` - If the coffer has been destroyed.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use memguard::Enclave;
    ///
    /// let random_key_enclave = Enclave::new_random(32).unwrap();
    /// assert_eq!(random_key_enclave.size(), 32);
    ///
    /// // The enclave can now be opened to get a Buffer with the random data.
    /// let key_buffer = random_key_enclave.open().unwrap();
    /// // ... use key_buffer ...
    /// key_buffer.destroy().unwrap();
    /// ```
    pub fn new_random(size: usize) -> Result<Self> {
        if size == 0 {
            return Err(MemguardError::OperationFailed(
                "Enclave random size must be positive".into(),
            ));
        }
        let mut random_buffer = Buffer::new_random(size)?;
        Self::seal(&mut random_buffer)
    }

    /// Seals a Buffer into an Enclave, encrypting its data.
    ///
    /// This function encrypts the data from the provided Buffer and returns it sealed
    /// inside an Enclave. The original Buffer is destroyed after encryption.
    ///
    /// # Arguments
    ///
    /// * `buffer` - A Buffer containing the sensitive data to encrypt
    ///
    /// # Returns
    ///
    /// * `Result<Self>` - A new Enclave containing the encrypted data
    ///
    /// # Errors
    ///
    /// * `MemguardError::SecretClosed` - If the buffer has been destroyed
    /// * Other errors if encryption fails
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use memguard::{Buffer, Enclave};
    ///
    /// // Create a memory manager
    /// let mut buffer = Buffer::new(32).unwrap();
    /// buffer.with_data_mut(|data| {
    ///     // Fill with data
    ///     for i in 0..data.len() {
    ///         data[i] = i as u8;
    ///     }
    ///     Ok(())
    /// }).unwrap();
    ///
    /// // Seal the buffer (this destroys the buffer)
    /// let enclave = Enclave::seal(&mut buffer).unwrap();
    /// assert!(!buffer.is_alive());
    /// ```
    pub fn seal(buffer: &mut Buffer) -> Result<Self> {
        // Check buffer status
        if !buffer.is_alive() {
            return Err(MemguardError::SecretClosed);
        }

        // Extract data and create enclave - this is our critical operation
        let result = buffer.with_data(|data| {
            // First check if we actually got data
            if data.is_empty() {
                return Err(MemguardError::OperationFailed(
                    "Buffer contains no data to seal".to_string(),
                ));
            }

            // Make a copy for encryption
            let mut data_copy = data.to_vec();

            // Create the enclave
            let enclave = Self::new(&mut data_copy)?;

            // Wipe our temporary copy
            data_copy.zeroize();

            Ok(enclave)
        });

        // Destroy the buffer regardless of success or failure
        // If this fails, prioritize returning the original operation result
        let destroy_result = buffer.destroy();

        match (result, destroy_result) {
            (Ok(enclave), Ok(())) => Ok(enclave),
            (Err(e), _) => Err(e),
            (Ok(_), Err(e)) => Err(e),
        }
    }

    /// Opens the Enclave and returns the decrypted data in a new Buffer.
    ///
    /// This function decrypts the data in the Enclave and returns it in a new Buffer.
    /// The Enclave itself is left unchanged and can be opened multiple times.
    ///
    /// # Returns
    ///
    /// * `Result<Buffer>` - A new Buffer containing the decrypted data
    ///
    /// # Errors
    ///
    /// * `MemguardError::SecretClosed` - If the coffer has been destroyed
    /// * `MemguardError::OperationFailed` - If decryption fails
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use memguard::{Buffer, Enclave};
    ///
    /// // Create a buffer with data and seal it
    /// let mut buffer = Buffer::new(32).unwrap();
    /// buffer.with_data_mut(|data| {
    ///     for i in 0..data.len() {
    ///         data[i] = i as u8;
    ///     }
    ///     Ok(())
    /// }).unwrap();
    ///
    /// let enclave = Enclave::seal(&mut buffer).unwrap();
    ///
    /// // Open the enclave
    /// let unsealed = enclave.open().unwrap();
    ///
    /// // Verify the data
    /// unsealed.with_data(|data| {
    ///     // Use the decrypted data
    ///     Ok(())
    /// }).unwrap();
    /// ```
    pub fn open(&self) -> Result<Buffer> {
        // Calculate the plaintext size (ciphertext - tag - nonce)
        if self.ciphertext.len() < OVERHEAD {
            return Err(MemguardError::CryptoError(
                "Ciphertext too short to contain data, tag, and nonce.".into(),
            ));
        }
        let plaintext_size = self.ciphertext.len() - OVERHEAD;

        // Create a new buffer to hold the decrypted data
        let buffer = Buffer::new(plaintext_size)?;

        // Get the encryption key from the coffer
        let coffer = globals::get_coffer();
        let coffer_guard = match coffer.lock() {
            Ok(guard) => guard,
            Err(_) => {
                // If locking fails, destroy the buffer
                let _ = buffer.destroy();
                return Err(MemguardError::OperationFailed(
                    "Failed to lock coffer".into(),
                ));
            }
        };

        let key_buffer = match coffer_guard.view() {
            Ok(kb) => kb,
            Err(e) => {
                // If key view fails, destroy the buffer
                let _ = buffer.destroy();
                return Err(e);
            }
        };

        // Decrypt the data into the buffer
        let decryption_result = key_buffer.with_data(|key_bytes| {
            buffer.with_data_mut(|buffer_data| {
                if key_bytes.len() != KEY_SIZE {
                    return Err(MemguardError::CryptoError("Invalid key size".into()));
                }

                // Extract the nonce (first NONCE_SIZE bytes)
                if self.ciphertext.len() < NONCE_SIZE {
                    // Should be caught by OVERHEAD check earlier
                    return Err(MemguardError::CryptoError(
                        "Ciphertext too short to contain nonce".to_string(),
                    ));
                }
                let nonce_bytes = &self.ciphertext[..NONCE_SIZE];
                let nonce_array = match <[u8; NONCE_SIZE]>::try_from(nonce_bytes) {
                    Ok(arr) => arr,
                    Err(_) => {
                        return Err(MemguardError::CryptoError(
                            "Invalid nonce format".to_string(),
                        ))
                    }
                };
                let nonce = Nonce::assume_unique_for_key(nonce_array);

                let actual_ciphertext_with_tag = &self.ciphertext[NONCE_SIZE..];
                if actual_ciphertext_with_tag.len() < TAG_SIZE {
                    // TAG_SIZE is the min length for ring AEAD output
                    return Err(MemguardError::CryptoError(
                        "Ciphertext (post-nonce) too short".to_string(),
                    ));
                }

                // Create the decryption key
                let unbound_key = match UnboundKey::new(chacha20_poly1305_alg(), key_bytes) {
                    Ok(key) => key,
                    Err(_) => {
                        return Err(MemguardError::CryptoError(
                            "Failed to create decryption key".to_string(),
                        ))
                    }
                };
                let less_safe_key = LessSafeKey::new(unbound_key);

                // Copy the actual ciphertext (after nonce, including tag) for in-place decryption
                let mut ciphertext_to_decrypt = actual_ciphertext_with_tag.to_vec();

                // Decrypt the data
                let plaintext = match less_safe_key.open_in_place(
                    nonce,
                    Aad::empty(),
                    &mut ciphertext_to_decrypt,
                ) {
                    Ok(pt) => pt,
                    Err(_) => {
                        return Err(MemguardError::CryptoError(
                            "Decryption failed (authentication failed)".to_string(),
                        ))
                    }
                };

                // Copy the decrypted data to the buffer
                buffer_data.copy_from_slice(plaintext);

                // Wipe the temporary data
                wipe(&mut ciphertext_to_decrypt);

                Ok(()) // Inner Ok for buffer.with_data_mut
            }) // Fix identity function usage with map_err
        }); // Fix identity function usage with map_err

        // Destroy key_buffer regardless of decryption outcome.
        if let Err(e) = key_buffer.destroy() {
            log::warn!("Failed to destroy key buffer during Enclave::open: {}", e);
            // If decryption_result is Ok, this warning is relevant.
            // If decryption_result is Err, that's the primary error.
        }

        match decryption_result {
            Ok(_) => {
                // Go's memguard.Enclave.Open returns a frozen buffer.
                buffer.freeze()?; // Ensure buffer is ReadOnly after opening.
                Ok(buffer)
            }
            Err(e) => {
                let _ = buffer.destroy(); // Destroy output buffer on decryption failure.
                Err(e)
            }
        }
    }

    /// Returns the size of the plaintext data stored in the Enclave.
    ///
    /// # Returns
    ///
    /// * `usize` - The size of the plaintext in bytes
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use memguard::{Buffer, Enclave};
    ///
    /// let mut buffer = Buffer::new(42).unwrap();
    /// let enclave = Enclave::seal(&mut buffer).unwrap();
    ///
    /// assert_eq!(enclave.size(), 42);
    /// ```
    pub fn size(&self) -> usize {
        self.ciphertext.len().saturating_sub(OVERHEAD)
    }
}

impl Drop for Enclave {
    fn drop(&mut self) {
        // Wipe the ciphertext
        wipe(&mut self.ciphertext);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::util::scramble_bytes; // For creating test data
    use serial_test::serial;

    #[test]
    fn test_core_new_enclave() {
        // Reset global state for tests
        crate::globals::reset_for_tests();

        let mut data = vec![0u8; 32];
        scramble_bytes(&mut data);
        let data_original = data.clone();

        let enclave = Enclave::new(&mut data).expect("Enclave::new failed");

        // Check that the original data buffer was wiped
        assert!(
            data.iter().all(|&x| x == 0),
            "Original data buffer was not wiped"
        );
        assert_ne!(
            data, data_original,
            "Original data should differ after wipe"
        );

        // Verify the length of the ciphertext
        assert_eq!(
            enclave.ciphertext.len(),
            data_original.len() + OVERHEAD,
            "Ciphertext has unexpected length"
        );
        assert_eq!(enclave.size(), data_original.len(), "Enclave size mismatch");

        // Attempt with an empty data slice
        let mut empty_data: Vec<u8> = Vec::new();
        match Enclave::new(&mut empty_data) {
            Err(MemguardError::OperationFailed(msg)) => {
                assert!(msg.contains("Enclave data must not be empty"));
            }
            _ => panic!("Expected OperationFailed for empty data"),
        }
    }

    #[test]
    #[serial]
    fn test_core_seal_and_open() {
        // Reset global state for tests
        crate::globals::reset_for_tests();

        // Create a new buffer for testing
        let mut b = Buffer::new(32).expect("Buffer::new failed for seal test");
        b.scramble().expect("Buffer scramble failed");
        let original_buffer_data = b.with_data(|d| Ok(d.to_vec())).unwrap();

        // Seal it into an Enclave
        let enclave = Enclave::seal(&mut b).expect("Enclave::seal failed");

        // Check ciphertext length
        assert_eq!(
            enclave.ciphertext.len(),
            original_buffer_data.len() + OVERHEAD,
            "Ciphertext length after seal is incorrect"
        );

        // Check that the buffer was destroyed
        assert!(!b.is_alive(), "Buffer was not consumed/destroyed by seal");

        // Open the enclave into a new buffer
        let opened_buffer = enclave.open().expect("Enclave::open failed");

        // Check that the decrypted data is correct
        opened_buffer
            .with_data(|decrypted_data| {
                assert_eq!(
                    decrypted_data,
                    original_buffer_data.as_slice(),
                    "Decrypted data does not match original"
                );
                Ok(())
            })
            .unwrap();

        // Attempt sealing a destroyed buffer
        // b is already destroyed. Let's create a new one and destroy it.
        let mut b2 = Buffer::new(16).expect("Buffer::new for destroyed seal test failed");
        b2.destroy().expect("Destroy for b2 failed");
        match Enclave::seal(&mut b2) {
            Err(MemguardError::SecretClosed) => { /* Expected */ }
            _ => panic!("Expected SecretClosed error when sealing a destroyed buffer"),
        }

        opened_buffer
            .destroy()
            .expect("Destroy for opened_buffer failed");
    }

    #[test]
    #[serial]
    fn test_core_open_modified_ciphertext() {
        // Reset global state for tests
        crate::globals::reset_for_tests();

        let mut data = vec![0u8; 32];
        scramble_bytes(&mut data);
        let mut enclave = Enclave::new(&mut data).expect("Enclave::new for modified test failed");

        // Modify the ciphertext to trigger an error case
        if !enclave.ciphertext.is_empty() {
            enclave.ciphertext[0] = !enclave.ciphertext[0]; // Flip a bit
        }

        // Check for the error
        match enclave.open() {
            Err(MemguardError::CryptoError(msg)) => {
                assert!(
                    msg.contains("Decryption failed") || msg.contains("authentication failed"),
                    "Expected decryption error, got: {}",
                    msg
                );
            }
            Ok(opened_buffer) => {
                opened_buffer.destroy().ok(); // Clean up if open unexpectedly succeeded
                panic!("Opening modified ciphertext should fail");
            }
            Err(e) => panic!(
                "Unexpected error type when opening modified ciphertext: {:?}",
                e
            ),
        }
    }

    #[test]
    fn test_core_enclave_size() {
        let enclave = Enclave {
            ciphertext: vec![0u8; 1234],
        }; // Direct construction for test
        assert_eq!(enclave.size(), 1234 - OVERHEAD, "EnclaveSize incorrect");
    }
}

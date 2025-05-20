use crate::buffer::Buffer;
use crate::error::MemguardError;
use crate::util::{hash, wipe};
use log::error;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Duration;

type Result<T> = std::result::Result<T, MemguardError>;

// Interval for re-keying (milliseconds)
const REKEY_INTERVAL_MS: u64 = 500;

/// A specialized, secure container for a 32-byte master encryption key.
///
/// The `Coffer` is central to `memguard`'s `Enclave` encryption. It holds the root
/// encryption key used for all enclave operations. The key material within the `Coffer`
/// is protected by splitting it across multiple internal `Buffer`s and using XOR techniques
/// to reconstruct it only when needed.
///
/// A background thread automatically and periodically "re-keys" the `Coffer`. This process
/// changes the internal representation of the stored key without altering its actual value,
/// making it more resilient against memory snapshot attacks.
///
/// Instances of `Coffer` are typically managed globally and implicitly by `memguard`
/// (see `globals::get_coffer`). Direct instantiation is usually for testing or advanced scenarios.
///
/// # Security Features
///
/// - **Split Storage**: The key is stored across multiple secure buffers
/// - **XOR Technique**: Uses XOR operations to reconstruct the key only when needed
/// - **Regular Re-keying**: Periodically changes the key representation without changing the value
/// - **Self-contained**: Does not expose the raw key material
///
/// # Examples
///
/// ```rust,no_run
/// use memguard::Buffer;
/// use std::time::Duration;
///
/// // NOTE: Coffer is internal to memguard and not exposed publicly.
/// // This is a hypothetical example of how it would be used internally.
///
/// // In actual usage, Coffer is managed internally by memguard
/// // and accessed through Buffer, Enclave, and other public types.
///
/// // let coffer = Coffer::new().unwrap();
/// // let key_view = coffer.view().unwrap();
/// // key_view.with_data(|key_bytes| {
/// //     assert_eq!(key_bytes.len(), 32);
/// //     Ok(())
/// // }).unwrap();
///
/// ```
#[derive(Debug)]
pub struct Coffer {
    // Split key components
    left: Buffer,
    right: Buffer,
    rand: Buffer,

    // Re-keying handle - used to detect destroyed status when the rekey thread needs to exit
    destroyed: Arc<AtomicBool>,
}

impl Coffer {
    /// Creates a new `Coffer` initialized with a random 32-byte master key.
    ///
    /// This method performs several critical setup steps:
    /// 1. Allocates secure `Buffer`s for the key's components.
    /// 2. Initializes these buffers with a cryptographically random key using a split-key XOR technique.
    /// 3. Spawns a background thread that periodically calls `rekey()` on this `Coffer` instance.
    ///
    /// # Returns
    ///
    /// * `Result<Self>` - A new `Coffer` instance.
    ///
    /// # Errors
    ///
    /// Returns `MemguardError` if `Buffer` allocation or initial key generation fails.
    ///
    /// # Panics
    ///
    /// The background re-keying thread will panic if `globals::get_coffer()` (which it uses
    /// to access the coffer instance for re-keying) encounters a poisoned mutex or fails
    /// during its own initialization.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// // This demonstrates direct creation, though typically Coffer is managed globally.
    /// // Ensure memguard::purge() is called if you manually manage Coffers outside the global one
    /// // and want their rekey threads to stop cleanly, or handle their lifecycle carefully.
    ///
    /// // let coffer = memguard::Coffer::new().expect("Failed to create coffer");
    /// // ... use coffer ...
    /// // coffer.destroy().expect("Failed to destroy coffer"); // Important for cleanup
    /// ```
    pub fn new() -> Result<Self> {
        // Create secure buffers for key storage
        let left = Buffer::new(32)?;
        let right = Buffer::new(32)?;
        let rand = Buffer::new(32)?;

        let destroyed = Arc::new(AtomicBool::new(false));

        let coffer = Self {
            left,
            right,
            rand,
            destroyed: destroyed.clone(),
        };

        // Initialize the key and handle any errors
        // Coffer::new now returns Result, so this init must succeed or new fails.
        // The coffer.init() call itself can return an error, which is propagated by `?`
        coffer.init()?;

        // Start re-keying thread
        let rekey_thread_destroyed_signal = destroyed.clone(); // Use a more descriptive name
        thread::spawn(move || {
            let sleep_duration = Duration::from_millis(REKEY_INTERVAL_MS);

            while !rekey_thread_destroyed_signal.load(Ordering::Relaxed) {
                thread::sleep(sleep_duration);
                if rekey_thread_destroyed_signal.load(Ordering::Relaxed) {
                    break;
                } // Check again after sleep

                // Check if we're shutting down
                if crate::globals::is_shutdown_in_progress() {
                    break;
                }

                // Access coffer through global instance
                let coffer_mutex = crate::globals::get_coffer();

                // LOCK ORDERING: Use try_lock with a timeout to avoid potential deadlocks
                // If we can't get the lock quickly, skip this rekey cycle and try again later
                match coffer_mutex.try_lock() {
                    Ok(coffer_guard) => {
                        if coffer_guard.destroyed() {
                            // Check if coffer itself was destroyed by another thread
                            break;
                        }
                        if let Err(e) = coffer_guard.rekey() {
                            // If rekey returns Err(MemguardError::SecretClosed), it's a clean exit.
                            if matches!(e, MemguardError::SecretClosed) {
                                break;
                            }
                            error!("Error during re-keying: {:?}", e);
                            break;
                        }
                    }
                    Err(e) => {
                        // Instead of checking if poisoned (which TryLockError doesn't support),
                        // just log the error and continue for simplicity
                        log::debug!("Rekey cycle skipped: coffer is currently in use by another thread. Error: {}", e);
                    }
                }
            }
        });

        Ok(coffer) // Coffer::new now returns Result
    }

    /// Initializes or resets the Coffer with a new random key.
    ///
    /// This overwrites any existing key with a new randomly generated one.
    ///
    /// # Returns
    ///
    /// * `Result<()>` - Ok if initialization succeeded
    ///
    /// # Errors
    ///
    /// * `MemguardError::SecretClosed` - If the Coffer has been destroyed
    /// * Other errors if buffer operations fail
    fn init(&self) -> Result<()> {
        if self.destroyed() {
            return Err(MemguardError::SecretClosed);
        }

        // Generate random data for both halves
        self.left.scramble()?;
        self.right.scramble()?;

        // left = left XOR hash(right)
        // Need to handle nested Result from with_data and closure
        self.right.with_data(|right_data| {
            self.left.with_data_mut(|left_data| {
                let mut right_hash = hash(right_data);
                for i in 0..left_data.len() {
                    left_data[i] ^= right_hash[i];
                }
                wipe(&mut right_hash); // Wipe temporary hash
                Ok(()) // For left.with_data_mut's closure
            }) // Fix identity function usage with map_err
        })?; // Flatten Result<Result<(), E>, E> from right.with_data and propagate error

        Ok(())
    }

    /// Provides a temporary, decrypted view of the master key for cryptographic operations.
    ///
    /// This method reconstructs the 32-byte master key from its internal split components
    /// and returns it in a new, secure `Buffer`. This `Buffer` is initially mutable
    /// but should ideally be frozen or destroyed by the caller as soon as it's no longer needed.
    ///
    /// **Important:** The returned `Buffer` contains the plaintext master key. It is the
    /// caller's responsibility to handle this `Buffer` securely and call `destroy()` on it
    /// promptly after use to wipe the key material from memory.
    ///
    /// # Returns
    ///
    /// * `Result<Buffer>` - A new `Buffer` containing the 32-byte master key.
    ///
    /// # Errors
    ///
    /// * `MemguardError::SecretClosed` - If this `Coffer` instance has been destroyed.
    /// * Other `MemguardError` variants if `Buffer` allocation or internal data access fails.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// // Assuming `coffer` is an existing Coffer instance.
    /// // let coffer = memguard::Coffer::new().unwrap();
    ///
    /// // let key_view_buffer = coffer.view().expect("Failed to get key view");
    /// // key_view_buffer.with_data(|key_bytes| {
    /// //     assert_eq!(key_bytes.len(), 32);
    /// //     // Use key_bytes for an encryption/decryption operation...
    /// //     Ok(())
    /// // }).unwrap().unwrap();
    /// //
    /// // // CRITICAL: Destroy the key view buffer immediately after use.
    /// // key_view_buffer.destroy().expect("Failed to destroy key view buffer");
    /// // coffer.destroy().unwrap(); // If coffer is manually managed
    /// ```
    pub fn view(&self) -> Result<Buffer> {
        if self.destroyed() {
            return Err(MemguardError::SecretClosed);
        }

        // Create a new buffer to hold the reconstructed key
        let key_buffer = Buffer::new(32)?;

        // Reconstruct the key: key = hash(right) XOR left
        self.right.with_data(|right_data| {
            self.left.with_data(|left_data| {
                key_buffer.with_data_mut(|key_data| {
                    let mut right_hash = hash(right_data);
                    for i in 0..key_data.len() {
                        key_data[i] = right_hash[i] ^ left_data[i];
                    }
                    wipe(&mut right_hash); // Wipe temporary hash
                    Ok(()) // For key_buffer.with_data_mut's closure
                })
            })
        })?; // Flatten Result<Result<(), E>, E> from right.with_data and propagate error

        Ok(key_buffer)
    }

    /// Re-keys the Coffer, changing the key representation without changing the value.
    ///
    /// This method changes how the key is split and stored, making it harder for attackers
    /// to compromise the key even if they can read memory at specific times.
    ///
    /// # Returns
    ///
    /// * `Result<()>` - Ok if re-keying succeeded
    ///
    /// # Errors
    ///
    /// * `MemguardError::SecretClosed` - If the Coffer has been destroyed
    /// * Other errors if buffer operations fail
    pub(crate) fn rekey(&self) -> Result<()> {
        if self.destroyed() {
            return Err(MemguardError::SecretClosed);
        }

        // Generate new random data
        self.rand.scramble()?;

        // Hash the current right component for later use
        let right_hash_current = self.right.with_data(|right_data| Ok(hash(right_data)))?;

        // new_right = current_right XOR random
        self.right.with_data_mut(|right_data| {
            self.rand.with_data(|rand_data| {
                for i in 0..right_data.len() {
                    right_data[i] ^= rand_data[i];
                }
                Ok(())
            })?;
            Ok(())
        })?;

        // Hash the new right component
        let right_hash_new = self.right.with_data(|right_data| Ok(hash(right_data)))?;

        // new_left = current_left XOR hash(current_right) XOR hash(new_right)
        self.left.with_data_mut(|left_data| {
            for i in 0..left_data.len() {
                left_data[i] ^= right_hash_current[i] ^ right_hash_new[i];
            }
            Ok(())
        })?;

        // Wipe temporary data
        wipe(&mut right_hash_current.to_vec());
        wipe(&mut right_hash_new.to_vec());

        Ok(())
    }

    /// Securely destroys the `Coffer`, wiping all its internal key material.
    ///
    /// This method performs the following actions:
    /// 1. Signals the background re-keying thread to terminate.
    /// 2. Waits briefly to allow the re-keying thread to exit.
    /// 3. Destroys the internal `Buffer`s (`left`, `right`, `rand`) that hold the key components.
    ///
    /// After `destroy` is called, the `Coffer` is no longer usable. Any subsequent calls
    /// to methods like `view()` or `rekey()` will return `MemguardError::SecretClosed`.
    ///
    /// # Returns
    ///
    /// * `Result<()>` - `Ok(())` if destruction was successful.
    ///
    /// # Errors
    ///
    /// Returns `MemguardError` if any of the internal `Buffer` destruction operations fail.
    /// The first error encountered during the destruction of internal buffers is returned.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// // Assuming `coffer` is an existing Coffer instance.
    /// // let coffer = memguard::Coffer::new().unwrap();
    /// // ... use coffer ...
    /// // coffer.destroy().expect("Failed to destroy coffer");
    /// // assert!(coffer.destroyed());
    /// ```
    pub fn destroy(&self) -> Result<()> {
        // Signal the re-keying thread to exit - set this first
        self.destroyed.store(true, Ordering::SeqCst);

        // Sleep a tiny bit to give the rekey thread a chance to exit
        // This avoids race conditions where the thread might try to access buffers
        // while we're destroying them
        thread::sleep(Duration::from_millis(1));

        // Create a vec of results to track errors
        // Since self.left, self.right, self.rand are now direct Buffers,
        // and Coffer::destroy is called when the global COFFER Mutex is held,
        // we can call destroy on them directly. Buffer::destroy handles its own internal locking.
        let mut first_error: Option<MemguardError> = None;

        if let Err(e) = self.left.destroy() {
            error!("Error destroying left part of Coffer: {:?}", e);
            if first_error.is_none() {
                first_error = Some(e);
            }
        }
        if let Err(e) = self.right.destroy() {
            error!("Error destroying right part of Coffer: {:?}", e);
            if first_error.is_none() {
                first_error = Some(e);
            }
        }
        if let Err(e) = self.rand.destroy() {
            error!("Error destroying rand part of Coffer: {:?}", e);
            if first_error.is_none() {
                first_error = Some(e);
            }
        }

        if let Some(e) = first_error {
            Err(e)
        } else {
            Ok(())
        }
    }

    /// Checks if the `Coffer` has been destroyed.
    ///
    /// # Returns
    ///
    /// * `bool` - `true` if `destroy()` has been called (or initiated) on this `Coffer`, `false` otherwise.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// // Assuming `coffer` is an existing Coffer instance.
    /// // let coffer = memguard::Coffer::new().unwrap();
    /// // assert!(!coffer.destroyed());
    /// //
    /// // coffer.destroy().unwrap();
    /// // assert!(coffer.destroyed());
    /// ```
    pub fn destroyed(&self) -> bool {
        self.destroyed.load(Ordering::Relaxed)
    }

    /// Test helper to force the coffer into a destroyed state
    /// without actually destroying the internal buffers
    #[cfg(test)]
    pub fn force_destroy_for_test(&self) {
        self.destroyed.store(true, Ordering::SeqCst);
    }

    /// Test helper to create a new coffer that's already destroyed
    #[cfg(test)]
    pub fn new_destroyed_for_test() -> Self {
        let coffer = Self::new().expect("Failed to create test coffer");
        coffer.force_destroy_for_test();
        coffer
    }

    /// Test helper to reinitialize the coffer (for purge)
    #[cfg(test)]
    pub fn test_reinit(&self) -> Result<()> {
        // First, reset the destroyed flag to false since we're reinitializing
        self.destroyed.store(false, Ordering::SeqCst);
        // Then call init
        self.init()
    }
}

impl Drop for Coffer {
    fn drop(&mut self) {
        // If we're shutting down, just mark as destroyed and return
        if crate::globals::is_shutdown_in_progress() {
            self.destroyed.store(true, Ordering::SeqCst);
            return;
        }

        // During program shutdown, don't try to destroy
        // Just mark as destroyed to stop rekey thread
        self.destroyed.store(true, Ordering::SeqCst);

        // Give a brief moment for rekey thread to notice
        std::thread::sleep(std::time::Duration::from_millis(1));

        // Don't call destroy() during drop as it can cause issues
        // during static cleanup
    }
}

// hash is already imported at the top of the file

#[cfg(test)]
mod tests {
    use super::*;
    // Removed hash import from here as it's now at the top of the file.

    // Helper to create a Coffer for tests that doesn't spawn the rekey thread
    fn new_test_coffer() -> Coffer {
        // Create secure buffers for key storage
        let left = Buffer::new(32).expect("Failed to create left buffer");
        let right = Buffer::new(32).expect("Failed to create right buffer");
        let rand = Buffer::new(32).expect("Failed to create rand buffer");

        let destroyed = Arc::new(AtomicBool::new(false));

        let coffer = Coffer {
            left,
            right,
            rand,
            destroyed,
        };

        // Initialize the key
        coffer.init().expect("Failed to initialize test coffer");

        // Don't spawn rekey thread for tests
        coffer
    }

    #[cfg(test)]
    impl Coffer {
        // Test-only helper to get hashes of internal components
        fn internal_hashes_for_test(&self) -> Result<([u8; 32], [u8; 32])> {
            if self.destroyed() {
                return Err(MemguardError::SecretClosed);
            }
            let left_hash = self.left.with_data(|d| Ok(hash(d)))?;
            let right_hash = self.right.with_data(|d| Ok(hash(d)))?;
            Ok((left_hash, right_hash))
        }
    }

    #[test]
    fn test_coffer_new() {
        let coffer = new_test_coffer();
        assert!(
            !coffer.destroyed(),
            "Coffer should not be destroyed after new"
        );
        // Implicitly tests that coffer.init() was called and succeeded.

        // Verify that fields are the expected sizes.
        assert_eq!(coffer.left.size(), 32, "left buffer has unexpected size");
        assert_eq!(coffer.right.size(), 32, "right buffer has unexpected size");
        assert_eq!(coffer.rand.size(), 32, "rand buffer has unexpected size");

        // Verify that the data fields are not zeroed (init should scramble them).
        let left_is_zero = coffer
            .left
            .with_data(|d| Ok(d.iter().all(|&x| x == 0)))
            .unwrap();
        assert!(!left_is_zero, "left buffer is all zeros after init");

        let right_is_zero = coffer
            .right
            .with_data(|d| Ok(d.iter().all(|&x| x == 0)))
            .unwrap();
        assert!(!right_is_zero, "right buffer is all zeros after init");
    }

    #[test]
    fn test_coffer_init() {
        let coffer = new_test_coffer();
        let view1_data = coffer
            .view()
            .unwrap()
            .with_data(|d| Ok(d.to_vec()))
            .unwrap();
        coffer.init().expect("Coffer re-init failed");
        let view2_data = coffer
            .view()
            .unwrap()
            .with_data(|d| Ok(d.to_vec()))
            .unwrap();
        assert_ne!(
            view1_data, view2_data,
            "Coffer value should change after init"
        );
        coffer.destroy().expect("Coffer destroy failed");
        assert!(
            matches!(coffer.init(), Err(MemguardError::SecretClosed)),
            "init on destroyed coffer should fail with SecretClosed"
        );
    }

    #[test]
    fn test_coffer_view() {
        let coffer = new_test_coffer();

        let view = coffer.view().expect("Could not view coffer in test");

        view.with_data(|data| {
            assert_eq!(data.len(), 32);
            assert!(
                !data.iter().all(|&x| x == 0),
                "View data should not be all zeros"
            );
            Ok(())
        })
        .expect("Failed to access view data");
        view.destroy().expect("View destroy failed");

        coffer.destroy().expect("Coffer destroy failed");
        assert!(
            matches!(coffer.view(), Err(MemguardError::SecretClosed)),
            "view on destroyed coffer should fail with SecretClosed"
        );
    }

    #[test]
    fn test_multiple_views_same_key() {
        let coffer = new_test_coffer();

        let view1 = coffer.view().expect("Could not create first view");
        let view2 = coffer.view().expect("Could not create second view");

        let bytes1 = view1
            .with_data(|data| Ok(data.to_vec()))
            .expect("Could not read from first view");
        let bytes2 = view2
            .with_data(|data| Ok(data.to_vec()))
            .expect("Could not read from second view");

        assert_eq!(
            bytes1, bytes2,
            "Key views should contain the same key material before rekey"
        );
    }

    #[test]
    fn test_coffer_rekey() {
        let coffer = new_test_coffer();

        let view1 = coffer
            .view()
            .expect("Could not create first view before re-keying");
        let bytes1 = view1
            .with_data(|data| Ok(data.to_vec()))
            .expect("Could not read from first view");
        let (left_hash_before, right_hash_before) = coffer
            .internal_hashes_for_test()
            .expect("Failed to get internal hashes before rekey");

        coffer.rekey().expect("Re-keying failed");

        let view2 = coffer
            .view()
            .expect("Could not create second view after re-keying");
        let bytes2 = view2
            .with_data(|data| Ok(data.to_vec()))
            .expect("Could not read from second view");
        let (left_hash_after, right_hash_after) = coffer
            .internal_hashes_for_test()
            .expect("Failed to get internal hashes after rekey");

        assert_eq!(
            bytes1, bytes2,
            "Key value should be the same even after re-keying"
        );

        // Check that the internal representation changed
        assert_ne!(
            left_hash_before, left_hash_after,
            "Coffer.left internal data should change after rekey"
        );
        // It's possible for right_hash to be the same if rand XORs it back to original, but highly unlikely with 32 bytes.
        // A more robust check would be if (left_hash_before, right_hash_before) != (left_hash_after, right_hash_after)
        // For simplicity, checking individual components is usually sufficient.
        assert_ne!(
            right_hash_before, right_hash_after,
            "Coffer.right internal data should change after rekey"
        );
        coffer.destroy().expect("Coffer destroy failed");
        assert!(
            matches!(coffer.rekey(), Err(MemguardError::SecretClosed)),
            "rekey on destroyed coffer should fail with SecretClosed"
        );
    }

    #[test]
    fn test_coffer_destroy() {
        let coffer = new_test_coffer();

        coffer.destroy().expect("Coffer destroy failed");

        // After destroy, the coffer should be marked as destroyed
        assert!(
            coffer.destroyed(),
            "Coffer should be marked destroyed after destroy"
        );

        // After destroy, operations should fail
        assert!(
            matches!(coffer.view(), Err(MemguardError::SecretClosed)),
            "view on destroyed coffer should fail"
        );
        assert!(
            matches!(coffer.rekey(), Err(MemguardError::SecretClosed)),
            "rekey on destroyed coffer should fail"
        );

        // Internal buffers are destroyed
        assert!(!coffer.left.is_alive(), "Coffer.left should be destroyed");
        assert!(!coffer.right.is_alive(), "Coffer.right should be destroyed");
        assert!(!coffer.rand.is_alive(), "Coffer.rand should be destroyed");
    }
}

//! Manages global state for `memguard`, including the central encryption key `Coffer`
//! and the `BufferRegistry` for tracking active secure buffers.
//!
//! ## Core Dump Disabling
//!
//! This module, during the initialization of the global `Coffer`, attempts to disable
//! core dumps to prevent sensitive data from being written to disk in the event of a crash.
//!
//! - On **Unix-like systems**, it uses `libc::setrlimit` with `RLIMIT_CORE` to set the core
//!   dump size to zero.
//! - On **Windows**, disabling core dumps (specifically, Windows Error Reporting (WER) dumps)
//!   is more complex and typically managed by system-wide settings or specific APIs like
//!   `SetErrorMode` for certain crash dialogs. This library does not alter these global
//!   Windows settings. Applications requiring stricter control over crash dumps on Windows
//!   may need to configure WER or use Windows-specific APIs.

use crate::coffer::Coffer;
use crate::registry::BufferRegistry;
use log::warn;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Mutex, OnceLock};

// Lock ordering is critical to avoid deadlocks:
// When obtaining multiple locks, follow this strict order:
// 1. COFFER must always be acquired before BUFFERS
// 2. Never hold a Buffer.inner lock while attempting to acquire a global lock
// See LOCK_ORDERING.md for detailed documentation

// Global registry of all secure buffers
static BUFFERS: OnceLock<Mutex<BufferRegistry>> = OnceLock::new();

// Global encryption key container
static COFFER: OnceLock<Mutex<Coffer>> = OnceLock::new();

// Signal for shutdown in progress to prevent cleanup issues
#[cfg(not(test))]
static SHUTDOWN_IN_PROGRESS: AtomicBool = AtomicBool::new(false);

// Export for test initialization
#[cfg(test)]
pub static SHUTDOWN_IN_PROGRESS: AtomicBool = AtomicBool::new(false);

struct ShutdownGuard;

impl Drop for ShutdownGuard {
    fn drop(&mut self) {
        SHUTDOWN_IN_PROGRESS.store(true, Ordering::SeqCst);
    }
}

// Initialize a static shutdown guard that will run at program termination
static SHUTDOWN_GUARD: OnceLock<ShutdownGuard> = OnceLock::new();

/// Check if shutdown is in progress
pub(crate) fn is_shutdown_in_progress() -> bool {
    SHUTDOWN_IN_PROGRESS.load(Ordering::SeqCst)
}

/// Initialize the shutdown guard
fn ensure_shutdown_guard() {
    let _ = SHUTDOWN_GUARD.get_or_init(|| ShutdownGuard);
}

/// Gets the global buffer registry (or creates a new one if it doesn't exist)
#[cfg(not(test))]
pub(crate) fn get_buffer_registry() -> &'static Mutex<BufferRegistry> {
    ensure_shutdown_guard();
    BUFFERS.get_or_init(|| Mutex::new(BufferRegistry::new()))
}

/// Gets the global buffer registry (or creates a new one if it doesn't exist)
/// Made public for tests to access
#[cfg(test)]
pub fn get_buffer_registry() -> &'static Mutex<BufferRegistry> {
    ensure_shutdown_guard();
    BUFFERS.get_or_init(|| Mutex::new(BufferRegistry::new()))
}

/// Test helper to ensure globals are available
/// With ctor initialization, globals persist throughout tests
pub fn reset_for_tests() {
    // Ensure shutdown flag is false for tests
    SHUTDOWN_IN_PROGRESS.store(false, Ordering::SeqCst);
    // Force get the cofffer to ensure it's initialized
    let _ = get_coffer();
}

/// For lifecycle tests that need to test actual destruction
#[cfg(test)]
pub fn destroy_for_lifecycle_test() {
    if let Some(coffer_mutex) = COFFER.get() {
        if let Ok(coffer) = coffer_mutex.lock() {
            // Mark the coffer as destroyed
            coffer.force_destroy_for_test();
        }
    }
}

/// For lifecycle tests that need to test actual initialization
#[cfg(test)]
pub fn reset_for_lifecycle_test() {
    // We can't directly reset global state, but we can mark the coffer as destroyed
    destroy_for_lifecycle_test();
    // Reset shutdown flag
    SHUTDOWN_IN_PROGRESS.store(false, Ordering::SeqCst);
}

/// Gets the global coffer (or creates a new one if it doesn't exist)
pub(crate) fn get_coffer() -> &'static Mutex<Coffer> {
    COFFER.get_or_init(|| {
        // Attempt to disable core dumps on Unix-like systems.
        #[cfg(unix)]
        {
            let rlimit_core = libc::rlimit {
                rlim_cur: 0, // Set current limit to 0
                rlim_max: 0, // Set maximum limit to 0
            };
            if unsafe { libc::setrlimit(libc::RLIMIT_CORE, &rlimit_core) } != 0 {
                use log::warn;
                warn!(
                    "Failed to disable core dumps: {}",
                    std::io::Error::last_os_error()
                );
            }
        }

        // Initialize with regular coffer
        let coffer = Coffer::new().unwrap_or_else(|e| {
            panic!(
                "Failed to initialize global Coffer during OnceLock init: {:?}",
                e
            );
        });
        Mutex::new(coffer)
    });

    // After get_or_init, COFFER is guaranteed to be initialized.
    let coffer_mutex = COFFER
        .get()
        .expect("COFFER OnceLock should be initialized here");

    // Check if the Coffer instance *inside* the Mutex is destroyed.
    // If so, replace it with a new one. This handles the case after purge().
    {
        let mut coffer_guard = coffer_mutex.lock().unwrap_or_else(|poisoned| {
            // This is a severe state. If the mutex is poisoned, something went very wrong.
            log::error!(
                "Global Coffer mutex was poisoned. Panicking. Error: {}",
                poisoned
            );
            panic!("Global Coffer mutex was poisoned. Panicking. {}", poisoned);
        });

        if coffer_guard.destroyed() {
            log::debug!(
                "Global Coffer instance was found destroyed. Re-initializing with a new Coffer."
            );
            *coffer_guard = Coffer::new().unwrap_or_else(|e| {
                panic!("Failed to re-initialize destroyed global Coffer: {:?}", e);
            });
        }
    } // coffer_guard is dropped here, releasing the lock.

    // Return the static reference to the Mutex.
    coffer_mutex
}

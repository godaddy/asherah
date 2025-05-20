//! Signal handling module for securely cleaning up memory on program termination
//!
//! This module provides functionality to handle system signals (like SIGINT, SIGTERM)
//! and ensure that sensitive data is securely wiped before the program terminates.
//!
//! ## Features
//!
//! - Registers handlers for common termination signals (SIGINT, SIGTERM)
//! - Performs secure cleanup of all allocated secrets before termination
//! - Allows custom handlers to be registered for specific signals
//! - Thread-safe implementation that works correctly in multi-threaded applications
//!
//! ## Usage
//!
//! ```rust,no_run
//! use securememory::signal;
//!
//! // Register the default signal handlers
//! signal::catch_interrupt();
//!
//! // Register a custom handler for specific signals
//! signal::catch_signal(|sig| {
//!     println!("Received signal: {}", sig);
//! });
//!
//! // Continue with normal program execution...
//! ```
//!
//! ## Implementation
//!
//! When a signal is caught:
//! 1. The user-provided signal handler is executed (if any)
//! 2. All secure memory is wiped and freed
//! 3. The program terminates with exit code 1
//!
//! This ensures that no sensitive data remains in memory when the program exits,
//! even if terminated unexpectedly.

use std::process;
use std::sync::mpsc::{self, Receiver, Sender};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Once, OnceLock, RwLock,
};
use std::thread; // Added for thread::spawn

use crate::error::{Result, SecureMemoryError};

// Global variables
#[allow(dead_code)]
static INIT: Once = Once::new();
#[allow(dead_code)]
static INITIALIZED: AtomicBool = AtomicBool::new(false);
#[allow(dead_code)]
static SIGNAL_TX: OnceLock<Sender<SignalEvent>> = OnceLock::new();

/// Custom signal numbers to match the OS signals across platforms
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(i32)]
pub enum Signal {
    /// SIGINT - Terminal interrupt signal (Ctrl+C)
    Interrupt = 2,

    /// SIGTERM - Termination signal
    Terminate = 15,

    /// Other signal
    Other(i32),
}

impl From<i32> for Signal {
    fn from(signal: i32) -> Self {
        match signal {
            2 => Signal::Interrupt,
            15 => Signal::Terminate,
            n => Signal::Other(n),
        }
    }
}

impl From<Signal> for i32 {
    fn from(signal: Signal) -> Self {
        match signal {
            Signal::Interrupt => 2,
            Signal::Terminate => 15,
            Signal::Other(n) => n,
        }
    }
}

impl std::fmt::Display for Signal {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Signal::Interrupt => write!(f, "SIGINT"),
            Signal::Terminate => write!(f, "SIGTERM"),
            Signal::Other(n) => write!(f, "Signal {}", n),
        }
    }
}

/// Event sent to the signal handling thread
#[allow(dead_code)]
enum SignalEvent {
    /// Register a new signal handler
    RegisterHandler(Box<dyn Fn(Signal) + Send + 'static>),

    /// Signal received
    Signal(Signal),
}

/// Initialize the signal handling system
///
/// This function initializes the signal handling system. It creates a background
/// thread that will receive signal events and execute the appropriate handlers.
///
/// # Returns
///
/// Returns `Ok(())` if initialization was successful, or an error if it failed.
fn initialize() -> Result<()> {
    if INITIALIZED.load(Ordering::SeqCst) {
        return Ok(());
    }

    INIT.call_once(|| {
        // Create a channel for the signal handler
        let (tx, rx) = mpsc::channel();

        // Store the sender for later use
        let _ = SIGNAL_TX.set(tx);

        // Start a thread to handle signals
        thread::spawn(move || signal_handler_thread(rx));

        // Register the actual signal handlers with the OS
        setup_os_signal_handlers().expect("Failed to set up signal handlers");

        INITIALIZED.store(true, Ordering::SeqCst);
    });

    Ok(())
}

/// The signal handler thread function
///
/// This function runs in a background thread and processes signal events.
/// It executes the appropriate handler when a signal is received.
#[allow(dead_code)]
fn signal_handler_thread(rx: Receiver<SignalEvent>) {
    // The current handler function
    let handler_lock = RwLock::new(None::<Box<dyn Fn(Signal) + Send + 'static>>);

    // Process events from the channel
    while let Ok(event) = rx.recv() {
        match event {
            SignalEvent::RegisterHandler(handler) => {
                // Update the handler
                if let Ok(mut guard) = handler_lock.write() {
                    *guard = Some(handler);
                }
            }
            SignalEvent::Signal(signal) => {
                // Call the handler if one is registered
                if let Ok(guard) = handler_lock.read() {
                    if let Some(handler) = &*guard {
                        handler(signal);
                    }
                }

                // Perform cleanup
                purge();

                // Exit the process
                process::exit(1);
            }
        }
    }
}

/// Set up the OS-specific signal handlers
///
/// This function registers the signal handlers with the operating system.
/// It only registers handlers for SIGINT and SIGTERM, not memory-related signals.
#[allow(dead_code)]
fn setup_os_signal_handlers() -> Result<()> {
    // Sender to pass to signal handlers
    let tx = SIGNAL_TX
        .get()
        .ok_or_else(|| {
            SecureMemoryError::OperationFailed("Signal handler channel not initialized".to_string())
        })?
        .clone();

    // Register handler for SIGINT (Ctrl+C)
    let tx_clone = tx.clone();
    ctrlc::set_handler(move || {
        let _ = tx_clone.send(SignalEvent::Signal(Signal::Interrupt));
    })
    .map_err(|e| {
        SecureMemoryError::OperationFailed(format!("Failed to set SIGINT handler: {}", e))
    })?;

    use signal_hook::consts::signal::*;

    // Only register handler for SIGTERM - don't catch memory-related signals
    unsafe {
        signal_hook::low_level::register(SIGTERM, move || {
            let _ = tx.send(SignalEvent::Signal(Signal::Terminate));
        })
        .map_err(|e| {
            SecureMemoryError::OperationFailed(format!("Failed to set handler for SIGTERM: {}", e))
        })?;
    }

    Ok(())
}

/// Purge all secure memory
///
use crate::protected_memory::secret::{ProtectedMemorySecret, SECRET_REGISTRY}; // Added ProtectedMemorySecret, SECRET_REGISTRY
use crate::secret::Secret; // Added Secret trait

/// This function is called when a signal is received to ensure all secure
/// memory is wiped before the program terminates.
fn purge() {
    eprintln!("Purging all secure memory before termination...");
    log::info!("Purging all secure memory due to signal.");

    if let Some(registry_mutex) = SECRET_REGISTRY.get() {
        if let Ok(mut registry_guard) = registry_mutex.lock() {
            log::debug!(
                "Accessed secret registry. Found {} weak references.",
                registry_guard.len()
            );
            // Iterate and attempt to close live secrets.
            // We use retain to remove weak pointers that no longer point to live data.
            registry_guard.retain(|weak_secret_internal| {
                if let Some(strong_secret_internal) = weak_secret_internal.upgrade() {
                    // The secret is still alive, attempt to close it.
                    // Create a temporary ProtectedMemorySecret to call its close method.
                    let temp_secret = ProtectedMemorySecret {
                        inner: strong_secret_internal,
                    };

                    // Call close() which maps to close_impl().
                    // close_impl is idempotent.
                    if let Err(e) = temp_secret.close() {
                        log::error!("Error closing a secret during purge: {:?}", e);
                    } else {
                        log::trace!("Successfully closed a secret during purge.");
                    }
                    // Explicitly drop the temporary secret wrapper. The Arc<SecretInternal>
                    // it holds might still be pointed to by an application elsewhere,
                    // or this might be the last reference if only the registry held it.
                    drop(temp_secret);

                    // Keep the weak reference in the registry if it's still somehow alive
                    // (e.g. close failed, or another Arc exists).
                    // If close was successful and this was the last strong ref via upgrade,
                    // weak_secret_internal.strong_count() would be 0 after this.
                    // Retain will keep it if strong_count > 0 after our operations.
                    return weak_secret_internal.strong_count() > 0;
                }
                // The Arc<SecretInternal> has been dropped elsewhere, remove weak ptr.
                log::trace!("Pruned a dead weak reference from secret registry during purge.");
                false
            });
            log::debug!(
                "Secret registry pruned. {} weak references remaining.",
                registry_guard.len()
            );
        } else {
            log::error!(
                "Failed to lock SECRET_REGISTRY during purge. Secrets may not be cleaned up."
            );
        }
    } else {
        log::info!("SECRET_REGISTRY not initialized. No secrets to purge.");
    }
    eprintln!("Purge attempt complete.");
}

/// Register a signal handler
///
/// This function registers a handler that will be called when a signal is
/// received. The handler is passed the signal that was received.
///
/// # Arguments
///
/// * `handler` - A function or closure that will be called when a signal is received
///
/// # Returns
///
/// Returns `Ok(())` if the handler was registered successfully, or an error if
/// initialization failed.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::signal;
///
/// signal::catch_signal(|sig| {
///     println!("Received signal: {}", sig);
/// }).unwrap();
/// ```
pub fn catch_signal<F>(handler: F) -> Result<()>
where
    F: Fn(Signal) + Send + 'static,
{
    // Initialize if not already initialized
    initialize()?;

    // Get the sender
    let tx_opt = SIGNAL_TX.get();

    if let Some(tx) = tx_opt {
        // Send the handler to the signal handling thread
        tx.send(SignalEvent::RegisterHandler(Box::new(handler)))
            .map_err(|e| {
                SecureMemoryError::OperationFailed(format!(
                    "Failed to register signal handler: {}",
                    e
                ))
            })?;

        Ok(())
    } else {
        Err(SecureMemoryError::OperationFailed(
            "Signal handling system not initialized (SIGNAL_TX sender not set)".to_string(),
        ))
    }
}

/// Register a signal handler for interrupt signals
///
/// This is a convenience function that registers a handler for SIGINT.
/// It's equivalent to calling `catch_signal` with a handler that does nothing.
///
/// # Returns
///
/// Returns `Ok(())` if the handler was registered successfully, or an error if
/// initialization failed.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::signal;
///
/// // Register the default signal handler for interrupts
/// signal::catch_interrupt().unwrap();
/// ```
pub fn catch_interrupt() -> Result<()> {
    catch_signal(|_| {})
}

/// Register a signal handler for multiple specific signals
///
/// This function registers a handler for the specified signals.
///
/// # Arguments
///
/// * `handler` - A function or closure that will be called when a signal is received
/// * `signals` - A slice of signals to handle
///
/// # Returns
///
/// Returns `Ok(())` if the handler was registered successfully, or an error if
/// initialization failed.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::signal::{self, Signal};
///
/// signal::catch_signals(|sig| {
///     println!("Received signal: {}", sig);
/// }, &[Signal::Interrupt, Signal::Terminate]).unwrap();
/// ```
pub fn catch_signals<F>(handler: F, _signals: &[Signal]) -> Result<()>
where
    F: Fn(Signal) + Send + 'static,
{
    // This is a placeholder for future implementation
    // Currently, our simple architecture catches all signals and just routes them
    // to the registered handler, but this could be extended to have different
    // handlers for different signals

    catch_signal(handler)
}

/// Function to handle abnormal process termination
///
/// This function is used internally to handle abnormal process termination.
/// It performs the same cleanup as signal handlers, but can be called directly
/// in situations where signal handlers might not be triggered.
///
/// # Arguments
///
/// * `exit_code` - The exit code to use when terminating the process
pub fn exit(exit_code: i32) -> ! {
    // Purge all secure memory
    purge();

    // Exit the process
    process::exit(exit_code)
}

/// Function to panic safely, ensuring all secure memory is wiped
///
/// This function is used to panic while ensuring all secure memory is wiped.
/// It's similar to the standard `panic!` macro, but performs secure cleanup first.
///
/// # Arguments
///
/// * `msg` - The panic message
pub fn panic(msg: String) -> ! {
    // Purge all secure memory
    purge();

    // Panic with the provided message
    std::panic::panic_any(msg)
}

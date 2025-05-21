#![allow(clippy::print_stderr)]

use crate::error::MemguardError;
use crate::util::safe_exit;
use log::{error, info};
use signal_hook::consts::signal::SIGINT;
use signal_hook::iterator::Signals;
use std::sync::Arc;
use std::thread;

type Result<T> = std::result::Result<T, MemguardError>;

// Global static for the custom signal handler.
// Arc<Mutex<Option<...>>> might be too complex if we just need to swap it.
// An Arc<dyn Fn + Send + Sync> stored in an ArcSwap or similar might be better,
// but for simplicity with signal_hook's handler registration, we'll stick to
// re-registering, which means the handler itself doesn't need to be globally mutable
// in this fashion after registration. The `Signals` instance handles the current handler.
// What needs to be global is the `Signals` instance if we want to change handlers later,
// or we accept that each call to catch_signal/catch_interrupt creates a new listener thread.

// For simplicity and to align with typical signal handling patterns where you set it once,
// we'll assume each call to catch_signal or catch_interrupt will spawn its own thread
// and manage its own Signals instance. This avoids complex global state for the handler itself.

/// Catches specified signals (Unix-like systems only) and executes a custom handler,
/// followed by a safe program termination.
///
/// When one of the specified signals is received, the provided `handler` function
/// is executed. After the handler completes, `memguard::safe_exit(1)`
/// is called to terminate the program. `safe_exit` attempts to wipe all sensitive
/// data managed by `memguard` before exiting.
///
/// **Important:**
/// - This function is only effective on Unix-like systems due to its reliance on `signal-hook`.
/// - It spawns a new thread to listen for the specified signals. Calling it multiple
///   times for different signal sets will result in multiple listener threads.
/// - The `handler` is executed in the signal listener thread.
///
/// # Arguments
///
/// * `handler` - An `Arc<dyn Fn(i32) + Send + Sync>`: The function to execute when a signal is caught.
///   It receives the signal number (an `i32` corresponding to `libc` signal constants) as an argument.
/// * `signals_to_catch` - A slice of signal numbers (e.g., `&[signal_hook::consts::signal::SIGINT, signal_hook::consts::signal::SIGTERM]`)
///   to catch. These should typically be constants from the `signal_hook::consts::signal` module.
///
/// # Returns
///
/// * `Ok(())` - If the signal handler was successfully registered.
/// * `Err(MemguardError::OsError)` - If registering the signal handler fails (e.g., due to issues with `signal_hook` initializing).
///
/// # Panics
///
/// This function itself does not panic but returns an error on registration failure.
/// However, the spawned signal-handling thread will eventually call `memguard::safe_exit(1)`.
/// `safe_exit` calls `purge()`, which *can* panic if critical errors occur during
/// the destruction of `Buffer`s (e.g., a canary mismatch indicating a buffer overflow).
///
/// # Examples
///
/// ```rust,no_run
/// use memguard::{catch_signal, Buffer};
/// use std::sync::Arc;
/// use std::thread;
/// use std::time::Duration;
/// use signal_hook::consts::signal::SIGUSR1;
///
/// // Create a buffer that the signal handler might interact with or that needs cleanup.
/// let important_buffer = Buffer::new(1024).unwrap();
///
/// let custom_handler = Arc::new(|signal_code: i32| {
///     eprintln!("Custom handler: Caught signal {}! Cleaning up specific resources...", signal_code);
///     // Perform application-specific cleanup here if needed,
///     // though memguard::safe_exit will call memguard::purge() anyway.
/// });
///
/// if cfg!(unix) { // Signal handling as implemented is Unix-specific
///     catch_signal(custom_handler, &[SIGUSR1]).expect("Failed to set up SIGUSR1 handler");
///     // To test, you would send SIGUSR1 to this process from another terminal:
///     // kill -USR1 <pid>
///     println!("Signal handler set up for SIGUSR1. PID: {}", std::process::id());
///     // Keep the main thread alive to receive the signal
///     thread::sleep(Duration::from_secs(30));
/// }
/// // important_buffer will be cleaned up by its Drop or by purge() via safe_exit().
/// ```
pub fn catch_signal(
    handler: Arc<dyn Fn(i32) + Send + Sync>,
    signals_to_catch: &[i32],
) -> Result<()> {
    let mut signals = match Signals::new(signals_to_catch) {
        Ok(s) => s,
        Err(e) => {
            error!("Failed to create signal iterator: {}", e);
            // Convert std::io::Error to MemguardError::OsError or similar
            return Err(MemguardError::OsError(format!(
                "Failed to register signal handler: {}",
                e
            )));
        }
    };

    let signals_to_catch_owned = signals_to_catch.to_vec(); // Clone for thread

    thread::spawn(move || {
        // This function explicitly watches for signals
        match signals.into_iter().next() {
            Some(sig) => {
                info!("memguard: Caught signal {}. Running custom handler.", sig);
                handler(sig); // Execute the custom handler
                info!(
                    "memguard: Custom handler finished for signal {}. Exiting.",
                    sig
                );
                safe_exit(1); // Terminate after handler execution
            }
            None => {
                // This should not happen unless the iterator is closed
                error!("memguard: Signal iterator closed unexpectedly");
            }
        }
    });
    info!(
        "memguard: Registered custom signal handler for signals: {:?}",
        signals_to_catch_owned
    );
    Ok(())
}

/// Catches interrupt signals (specifically `SIGINT` on Unix-like systems) and handles them
/// by logging a message and then safely terminating the program.
///
/// This is a convenience wrapper around `catch_signal`. When `SIGINT` (typically generated
/// by Ctrl+C in a terminal) is caught, it logs a message indicating the event and then
/// calls `memguard::safe_exit(1)`. `safe_exit` attempts to wipe all sensitive data
/// managed by `memguard` before program termination.
///
/// **Important:**
/// - This function is only effective on Unix-like systems.
/// - A subsequent call to `catch_signal` with a custom handler for `SIGINT` may
///   override the behavior set by this function, as `signal-hook` typically allows
///   multiple handlers or replaces existing ones based on its internal logic.
///
/// # Returns
///
/// * `Ok(())` - If the interrupt handler was successfully registered.
/// * `Err(MemguardError::OsError)` - If registering the signal handler fails.
///
/// # Panics
///
/// Similar to `catch_signal`, this function does not panic directly. The termination path
/// via `safe_exit(1)` involves `purge()`, which can panic on critical buffer errors.
///
/// # Examples
///
/// ```rust,no_run
/// use memguard::{catch_interrupt, Buffer};
/// use std::thread;
/// use std::time::Duration;
///
/// if cfg!(unix) { // Signal handling as implemented is Unix-specific
///     catch_interrupt().expect("Failed to set up interrupt handler");
///     println!("Interrupt handler set up. Press Ctrl+C to test. PID: {}", std::process::id());
///     // Create some sensitive data
///     let sensitive_buffer = Buffer::new(64).unwrap();
///     // ... use sensitive_buffer ...
///     thread::sleep(Duration::from_secs(30)); // Keep alive
///     // sensitive_buffer will be cleaned up by purge() via safe_exit() if SIGINT is caught.
/// }
/// ```
pub fn catch_interrupt() -> Result<()> {
    catch_signal(
        Arc::new(|signal_code| {
            let message = format!(
                "memguard: Interrupt signal ({}) caught. Cleaning up...",
                signal_code
            );
            info!("{}", message);
            eprintln!("{}", message); // Also print to stderr for testability
        }),
        &[SIGINT],
    )
}

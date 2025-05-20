//! # memguard
//!
//! `memguard` is a Rust library providing secure memory storage for sensitive data,
//! ported from Go's `github.com/awnumar/memguard`.
//!
//! It aims to protect sensitive data from both accidental exposure and deliberate attacks
//! through features like:
//!
//! - **Secure Memory Allocation**: Uses `memcall-rs` for allocations with guard pages to detect overflows.
//! - **Memory Locking**: Prevents sensitive data from being swapped to disk.
//! - **Automatic Wiping**: Securely wipes memory when data is no longer needed (e.g., on `Drop` or explicit `destroy`).
//! - **Encryption Containers**: `Enclave` objects for encrypting data at rest in memory.
//! - **Key Management**: Internal `Coffer` system for managing encryption keys with periodic re-keying.
//! - **Stream Processing**: `Stream` API for handling large sensitive data in encrypted chunks.
//! - **Signal Handling**: Graceful cleanup of sensitive data on program termination signals.
//!
//! ## Key Components
//!
//! - `Buffer`: The fundamental secure memory container.
//! - `Enclave`: For encrypting data held within a `Buffer`.
//! - `Stream`: For reading and writing large data securely in encrypted chunks.
//! - `catch_interrupt()`, `catch_signal()`: For safe cleanup on signals.
//! - `purge()`: For emergency wiping of all sensitive data.
//!
//! ## Example Usage
//!
//! ```rust,no_run
//! use memguard::{Buffer, Enclave, Stream, catch_interrupt, purge, MemguardError};
//! use std::io::{Read, Write};
//!
//! // Invert the bytes in a buffer, using an Enclave for protection.
//! fn invert_buffer_securely(input_buffer: &mut Buffer) -> Result<Enclave, MemguardError> {
//!     // Make the buffer mutable if it wasn't (e.g., if it came from Enclave::open)
//!     input_buffer.melt()?;
//!
//!     input_buffer.with_data_mut(|data| {
//!         for byte in data.iter_mut() {
//!             *byte = !*byte;
//!         }
//!         Ok(())
//!     })?;
//!
//!     // Re-seal the modified buffer. This also destroys input_buffer.
//!     Enclave::seal(input_buffer)
//! }
//!
//! fn main() -> Result<(), Box<dyn std::error::Error>> {
//!     // Setup signal handling for graceful shutdown
//!     catch_interrupt().expect("Failed to set up interrupt handler");
//!
//!     // Defer purge for cleanup on normal exit (not strictly necessary if using catch_interrupt
//!     // for all exit paths, but good practice for applications).
//!     // Note: In a real app, you'd only call purge in main or on controlled shutdown.
//!     // For this example, we'll simulate its use.
//!     // defer_lite::defer! { memguard::purge(); } // Using a defer crate for example
//!
//!     // Create some initial sensitive data
//!     let mut original_data = vec![0x0F, 0xF0, 0xAA, 0x55];
//!     let mut initial_buffer = Buffer::new_from_bytes(&mut original_data)?;
//!
//!     println!("Original buffer created with size: {}", initial_buffer.size());
//!
//!     // Process it securely
//!     let processed_enclave = match invert_buffer_securely(&mut initial_buffer) {
//!         Ok(enclave) => enclave,
//!         Err(e) => {
//!             eprintln!("Error during secure inversion: {:?}", e);
//!             // In a real scenario, might call memguard::safe_panic here
//!             memguard::purge(); // Ensure cleanup on error path
//!             return Err(Box::new(e));
//!         }
//!     };
//!
//!     // initial_buffer is now destroyed because Enclave::seal consumes it.
//!     assert!(!initial_buffer.is_alive());
//!
//!     // Open the processed enclave to verify
//!     let final_buffer = processed_enclave.open()?;
//!     final_buffer.with_data(|data| {
//!         println!("Processed data: {:?}", data);
//!         assert_eq!(data, &[!0x0F, !0xF0, !0xAA, !0x55]);
//!         Ok(())
//!     })?;
//!
//!     println!("Successfully processed data.");
//!     final_buffer.destroy()?; // Explicitly destroy when done
//!
//!     // Example of Stream
//!     let mut stream = Stream::new();
//!     let stream_data = b"Large sensitive data for streaming...";
//!     stream.write_all(stream_data)?; // Use write_all from std::io::Write
//!     println!("Stream size: {}", stream.size());
//!     let (mut flushed_buffer, io_err_opt) = stream.flush()?;
//!     assert!(io_err_opt.is_none());
//!     flushed_buffer.with_data(|data| {
//!         assert_eq!(data, stream_data);
//!         Ok(())
//!     })?;
//!     flushed_buffer.destroy()?;
//!
//!     // On normal exit, if not using a defer macro, call purge manually if needed.
//!     // memguard::purge(); // Or rely on catch_interrupt for signal-based exits.
//!     Ok(())
//! }
//! ```

mod buffer;
mod coffer;
mod enclave;
mod error;
mod globals;

// Export for integration tests
pub use globals::reset_for_tests;
mod registry;
mod signals;
mod stream;
mod util;

pub use buffer::Buffer;
pub use enclave::Enclave;
pub use error::MemguardError;
pub use signals::{catch_interrupt, catch_signal};
pub use stream::{Stream, DEFAULT_STREAM_CHUNK_SIZE};
pub use util::{purge, safe_exit, safe_panic, scramble_bytes, wipe_bytes};

// No need to re-export globals since it's already pub mod in test mode

// Test-specific global initialization
#[cfg(test)]
use ctor::ctor;

#[cfg(test)]
#[ctor]
fn test_init() {
    use crate::globals::{get_buffer_registry, get_coffer, SHUTDOWN_IN_PROGRESS};
    use std::sync::atomic::Ordering;

    // Initialize logging for tests
    let _ = env_logger::builder()
        .filter_level(log::LevelFilter::Debug)
        .try_init();

    // Initialize all globals once at test startup
    let _ = get_coffer();
    let _ = get_buffer_registry();

    // Prevent shutdown during tests
    SHUTDOWN_IN_PROGRESS.store(false, Ordering::SeqCst);
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_buffer_simple() {
        // Create a buffer
        let buffer = Buffer::new(64).expect("Buffer creation failed in lib.rs test");

        // The buffer should be alive at this point
        assert!(buffer.is_alive(), "Buffer should be alive after creation");

        // Destroy it
        buffer
            .destroy()
            .expect("Buffer destruction failed in lib.rs test");

        // Verify it's destroyed
        assert!(
            !buffer.is_alive(),
            "Buffer should be destroyed after explicit destroy call"
        );
    }
}

#[cfg(test)]
mod test_debug;

#[cfg(test)]
mod test_debug2;

#[cfg(test)]
mod test_debug3;

#[cfg(test)]
mod test_debug4;

#[cfg(test)]
mod test_debug5;

#[cfg(test)]
mod test_debug6;

#[cfg(test)]
mod test_debug7;

#[cfg(test)]
mod test_debug8;

#[cfg(test)]
mod test_debug9;

#[cfg(test)]
mod test_debug10;

#[cfg(test)]
mod test_minimal;

#[cfg(test)]
mod test_init;

#[cfg(test)]
mod test_minimal2;

#[cfg(test)]
mod test_abort_issue;

#[cfg(test)]
mod test_single_thread;

#[cfg(test)]
mod test_empty_buffer;

#[cfg(test)]
mod test_debug_hash;

#[cfg(test)]
mod test_debug_hash2;

#[cfg(test)]
mod test_verify_go_hash;

#[cfg(test)]
mod test_abort_minimal;

#[cfg(test)]
mod test_abort_intercept;

#[cfg(test)]
mod test_abort_debug {
    use std::sync::Once;

    static INIT: Once = Once::new();

    #[test]
    fn debug_abort_location() {
        INIT.call_once(|| {
            use libc::{signal, SIGABRT, SIG_DFL};
            extern "C" fn abort_handler(sig: i32) {
                eprintln!("\n=== SIGABRT CAUGHT ===");
                eprintln!("Signal: {}", sig);
                eprintln!("Thread: {:?}", std::thread::current().name());

                // Print backtrace
                let backtrace = std::backtrace::Backtrace::capture();
                eprintln!("Backtrace:\n{}", backtrace);
                eprintln!("====================\n");

                // Reset to default handler and re-raise
                unsafe {
                    signal(SIGABRT, SIG_DFL);
                }
                std::process::abort();
            }

            unsafe {
                signal(SIGABRT, abort_handler as *const () as libc::sighandler_t);
            }
        });

        // Run a simple test that might trigger the abort
        let b = crate::Buffer::new(32).unwrap();
        b.destroy().unwrap();
    }
}

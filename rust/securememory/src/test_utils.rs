//! Utilities for testing securememory components
//!
//! This module provides helpers for creating isolated tests that avoid
//! interfering with each other when run in parallel or as part of a test suite.

use std::sync::Mutex;
use once_cell::sync::Lazy;

/// Global mutex to prevent multiple tests from accessing protected memory simultaneously
/// 
/// This helps prevent test flakiness by ensuring that one test fully completes
/// its memory operations before another test begins.
static TEST_MUTEX: Lazy<Mutex<()>> = Lazy::new(|| Mutex::new(()));

/// Helper struct to acquire an exclusive lock for protected memory tests
///
/// This struct uses RAII pattern to ensure the lock is held for the duration
/// of the test and automatically released when the test completes, even if
/// the test panics.
pub struct TestGuard {
    _lock: std::sync::MutexGuard<'static, ()>,
}

impl TestGuard {
    /// Create a new test guard for exclusive test execution
    ///
    /// # Returns
    ///
    /// A guard that holds the lock until it's dropped
    pub fn new() -> Self {
        let _lock = TEST_MUTEX.lock().expect("Failed to acquire test lock");
        Self { _lock }
    }
}

/// Macro to create an isolated test function
///
/// This macro wraps a test function to ensure it runs with exclusive access
/// to protected memory resources.
///
/// # Example
///
/// ```rust
/// # // Crate-relative import doesn't work in doc tests, so we fake it
/// # pub mod test_utils { pub struct TestGuard { } impl TestGuard { pub fn new() -> Self { Self {} } } }
/// # macro_rules! isolated_test { ($name:ident, $body:expr) => { fn $name() { let _guard = test_utils::TestGuard::new(); $body } } }
///
/// isolated_test!(my_test, {
///     // Test code that needs isolated access to protected memory
///     assert_eq!(2 + 2, 4);
/// });
/// ```
#[macro_export]
macro_rules! isolated_test {
    ($name:ident, $body:expr) => {
        #[test]
        fn $name() {
            let _guard = $crate::test_utils::TestGuard::new();
            $body
        }
    };
}

/// Helper function to verify a test is running in isolation
///
/// This is primarily useful for debugging test isolation issues.
///
/// # Returns
///
/// true if the test is properly isolated, false otherwise
pub fn verify_test_isolation() -> bool {
    // Try to acquire the lock without blocking
    TEST_MUTEX.try_lock().is_err()
}
# SecureMemory Tests

This directory contains tests for the `securememory` crate. These tests validate the functionality, safety, and performance of the secure memory management components.

## Test Isolation

Many tests in this directory access and manipulate protected memory, which can lead to race conditions and test flakiness when running tests in parallel. To address this, we provide test isolation utilities:

### Using TestGuard

```rust
use securememory::test_utils::TestGuard;

#[test]
fn my_protected_memory_test() {
    // Create a guard that ensures this test has exclusive access
    let _guard = TestGuard::new();
    
    // Test code here...
}
```

### Using the isolated_test! Macro

```rust
use securememory::isolated_test;

isolated_test!(my_test_name, {
    // Test code here automatically gets exclusive access
    // to protected memory resources
});
```

## Test Categories

The tests are organized into several categories:

1. **Basic Functionality Tests**: Tests basic operations like creating, accessing, and closing secrets.

2. **Concurrency Tests**: Tests that verify thread-safety and proper handling of concurrent access.

3. **Edge Case Tests**: Tests for handling error conditions and edge cases.

4. **Memory Protection Tests**: Tests to verify proper memory protection against unauthorized access.

5. **Signal Handling Tests**: Tests for proper handling of signals and interrupts.

## Running Tests

To run all tests:

```bash
cargo test
```

To run a specific test:

```bash
cargo test test_name
```

To run tests with extra verbosity (to see println! output):

```bash
cargo test -- --nocapture
```

## Memory Testing

Some tests involve checking for memory leaks and proper memory cleanup. These tests can be run with tools like Valgrind on Linux systems:

```bash
valgrind --leak-check=full cargo test test_name
```

## Test Isolation Notes

When adding new tests that interact with protected memory, make sure to:

1. Use the `TestGuard` or `isolated_test!` macro to ensure isolation
2. Close secrets explicitly when done
3. Be careful with multiple threads that access the same secret
4. Avoid global state when possible

These measures help prevent test flakiness and ensure consistent test results.
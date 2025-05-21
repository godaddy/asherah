//! Tests that specifically test the lifecycle (initialization and teardown) of global state
//! These tests must run sequentially to avoid interference

use serial_test::serial;

// Note: These tests are all marked with #[serial] to make sure they run sequentially
// and reset global state between tests. They intentionally manipulate global state
// in ways that would interfere with other tests if run concurrently.

#[test]
#[serial]
fn test_buffer_operations_after_purge() {
    // Reset global state before test
    memguard::reset_for_tests();

    // Create a buffer before purge
    let buffer = memguard::Buffer::new(32).expect("Failed to create test buffer");

    // Fill with test data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = i as u8;
            }
            Ok(())
        })
        .expect("Failed to write test data");

    // Call purge which should destroy all buffers
    memguard::purge();

    // Verify the buffer is now destroyed
    assert!(!buffer.is_alive(), "Buffer should be destroyed after purge");

    // Attempt to access the buffer - should fail with SecretClosed
    let buffer_access_result = buffer.with_data(|_| Ok(()));
    assert!(
        matches!(
            buffer_access_result,
            Err(memguard::MemguardError::SecretClosed)
        ),
        "Buffer access should fail with SecretClosed after purge"
    );

    // Verify we can still create new buffers after purge
    // (Coffer should be re-initialized as needed)
    let new_buffer = memguard::Buffer::new(32).expect("Failed to create new buffer after purge");
    assert!(new_buffer.is_alive(), "New buffer should be alive");

    // Verify the new buffer works correctly
    new_buffer
        .with_data_mut(|data| {
            data[0] = 42;
            Ok(())
        })
        .expect("Failed to write to new buffer after purge");

    new_buffer
        .with_data(|data| {
            assert_eq!(data[0], 42, "Data not correctly written to new buffer");
            Ok(())
        })
        .expect("Failed to read from new buffer after purge");

    // Clean up
    new_buffer.destroy().expect("Failed to destroy new buffer");
}

#[test]
#[serial]
fn test_coffer_view_after_destroy() {
    // Reset global state before test
    memguard::reset_for_tests();

    // This test requires the ability to access global state internals
    // which is not exposed in the public API. This test is a placeholder
    // for when we have proper lifecycle test support.

    // What this test should do:
    // 1. Force destroy the global coffer
    // 2. Verify that view() returns SecretClosed error
}

#[test]
#[serial]
fn test_coffer_rekey_after_destroy() {
    // Reset global state before test
    memguard::reset_for_tests();

    // This test requires the ability to access global state internals
    // which is not exposed in the public API. This test is a placeholder
    // for when we have proper lifecycle test support.

    // What this test should do:
    // 1. Force destroy the global coffer
    // 2. Verify that rekey() returns SecretClosed error
}

#[test]
#[serial]
fn test_buffer_after_coffer_destroy() {
    // Reset global state before test
    memguard::reset_for_tests();

    // This test requires the ability to access global state internals
    // which is not exposed in the public API. This test is a placeholder
    // for when we have proper lifecycle test support.

    // What this test should do:
    // 1. Create a buffer while coffer is active
    // 2. Force destroy the global coffer
    // 3. Verify existing buffers still work
    // 4. Verify creating new buffers fails
}

#[test]
#[serial]
fn test_enclave_operations_after_purge() {
    // Reset global state before test
    memguard::reset_for_tests();

    // Create an enclave before purge
    let enclave = memguard::Enclave::new_random(32).expect("Failed to create test enclave");

    // Call purge which should destroy all secure memory
    memguard::purge();

    // Attempt to open the enclave - should fail since global state was purged
    // Failure could be either SecretClosed or a crypto error depending on implementation details
    match enclave.open() {
        Err(memguard::MemguardError::SecretClosed) => {
            // This is an expected behavior
            println!("Got SecretClosed as expected");
        }
        Err(memguard::MemguardError::CryptoError(_)) => {
            // This is also an expected behavior since the keys were purged
            println!("Got CryptoError as expected");
        }
        Err(e) => {
            panic!(
                "Expected SecretClosed or CryptoError, got different error: {:?}",
                e
            );
        }
        Ok(_) => {
            panic!("Enclave open succeeded after purge, expected failure");
        }
    }

    // Verify we can still create new enclaves after purge
    // (Coffer should be re-initialized as needed)
    let new_enclave =
        memguard::Enclave::new_random(32).expect("Failed to create new enclave after purge");

    // Verify the new enclave works correctly
    let buffer = new_enclave
        .open()
        .expect("Failed to open new enclave after purge");
    assert_eq!(buffer.size(), 32, "New enclave has incorrect size");

    buffer
        .with_data(|data| {
            // Should contain random data (not all zeros)
            assert!(
                data.iter().any(|&x| x != 0),
                "Enclave data should be random, not all zeros"
            );
            Ok(())
        })
        .expect("Failed to read data from new enclave's buffer");

    // Clean up
    buffer
        .destroy()
        .expect("Failed to destroy buffer from new enclave");
}

#[test]
#[serial]
fn test_stream_operations_after_purge() {
    use std::io::Write;

    // Reset global state before test
    memguard::reset_for_tests();

    // Create a stream before purge
    let mut stream = memguard::Stream::new();

    // Write some initial data to the stream
    let initial_data = b"Initial data before purge";
    stream
        .write_all(initial_data)
        .expect("Failed to write initial data to stream");

    // Call purge which should destroy all secure memory
    memguard::purge();

    // Attempt to write more data to the stream after purge
    let additional_data = b"Additional data after purge";
    match stream.write_all(additional_data) {
        Err(e) => {
            // Expected behavior is to fail, but the exact error might vary
            // It could be SecretClosed or an I/O error depending on how Stream is implemented
            println!("Stream write failed after purge as expected: {:?}", e);
        }
        Ok(_) => {
            // If write succeeds, flush should fail
            match stream.flush_stream() {
                Err(e) => {
                    println!("Stream flush failed after purge as expected: {:?}", e);
                }
                Ok(_) => {
                    panic!("Stream operations succeeded after purge, expected failure");
                }
            }
        }
    }

    // Verify we can still create new streams after purge
    let mut new_stream = memguard::Stream::new();

    // Verify the new stream works correctly
    let test_data = b"Test data for new stream after purge";
    new_stream
        .write_all(test_data)
        .expect("Failed to write to new stream after purge");

    // Flush the stream to get the buffer with all data
    let (buffer, io_err) = new_stream
        .flush_stream()
        .expect("Failed to flush new stream after purge");
    assert!(io_err.is_none(), "I/O error during new stream flush");

    // Verify the data in the buffer
    buffer
        .with_data(|data| {
            assert_eq!(
                data, test_data,
                "Data in flushed buffer doesn't match written data"
            );
            Ok(())
        })
        .expect("Failed to read data from new stream's buffer");

    // Clean up
    buffer
        .destroy()
        .expect("Failed to destroy buffer from new stream");
}

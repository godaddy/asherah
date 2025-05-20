//! Tests that can safely run concurrently
//! These tests DO NOT test lifecycle management (init/destroy)
//! of the global state.

use memguard::{scramble_bytes, wipe_bytes, Buffer, MemguardError, Stream};
use std::io::Write;

// Simple buffer creation and destruction
#[test]
fn test_concurrent_buffer_basic() {
    let buffer = Buffer::new(64).expect("Buffer creation failed");
    assert_eq!(buffer.size(), 64);
    assert!(buffer.is_alive());

    // Write data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = i as u8;
            }
            Ok(())
        })
        .expect("Writing to buffer failed");

    // Read data
    buffer
        .with_data(|data| {
            for i in 0..data.len() {
                assert_eq!(data[i], i as u8);
            }
            Ok(())
        })
        .expect("Reading from buffer failed");

    buffer.destroy().expect("Buffer destruction failed");
    assert!(!buffer.is_alive());
}

// Test buffer cloning
#[test]
fn test_concurrent_buffer_clone() {
    let buffer = Buffer::new(32).expect("Buffer creation failed");

    // Write data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = (i * 2) as u8;
            }
            Ok(())
        })
        .expect("Writing to buffer failed");

    // Clone the buffer
    let buffer_clone = buffer.clone();

    // Verify both buffers have the same data
    buffer
        .with_data(|data| {
            buffer_clone
                .with_data(|clone_data| {
                    assert_eq!(data, clone_data);
                    Ok(())
                })
                .expect("Reading from clone failed");
            Ok(())
        })
        .expect("Reading from original failed");

    // Destroying the original should not affect the clone
    buffer
        .destroy()
        .expect("Original buffer destruction failed");
    assert!(!buffer.is_alive());
    assert!(buffer_clone.is_alive());

    // Clone should still have the data
    buffer_clone
        .with_data(|data| {
            for i in 0..data.len() {
                assert_eq!(data[i], (i * 2) as u8);
            }
            Ok(())
        })
        .expect("Reading from clone after original destroyed failed");

    buffer_clone
        .destroy()
        .expect("Clone buffer destruction failed");
}

// Test buffer random creation
#[test]
fn test_concurrent_buffer_random() {
    let random_buffer = Buffer::new_random(48).expect("Random buffer creation failed");
    assert_eq!(random_buffer.size(), 48);

    // Random buffer should not be all zeroes
    let is_all_zeroes = random_buffer
        .with_data(|data| Ok(data.iter().all(|&x| x == 0)))
        .expect("Reading from random buffer failed");

    assert!(!is_all_zeroes, "Random buffer should not be all zeroes");

    // Random buffers should be read-only by default
    assert!(matches!(
        random_buffer.with_data_mut(|_| Ok(())),
        Err(MemguardError::ProtectionFailed(_))
    ));

    // But can be made mutable
    random_buffer
        .melt()
        .expect("Making random buffer mutable failed");
    random_buffer
        .with_data_mut(|data| {
            data[0] = 0xFF;
            Ok(())
        })
        .expect("Writing to melted random buffer failed");

    random_buffer
        .destroy()
        .expect("Random buffer destruction failed");
}

// Test stream basic operations
#[test]
fn test_concurrent_stream_basic() {
    let mut stream = Stream::new();

    // Write data
    let data = b"Test data for the stream functionality";
    stream.write_all(data).expect("Writing to stream failed");

    // Verify size
    assert_eq!(stream.size(), data.len());

    // Flush data to a buffer
    let (buffer, io_err) = stream.flush().expect("Flushing stream failed");
    assert!(io_err.is_none());

    // Verify buffer contains the data
    buffer
        .with_data(|buffer_data| {
            assert_eq!(buffer_data, data);
            Ok(())
        })
        .expect("Reading from flushed buffer failed");

    buffer.destroy().expect("Buffer destruction failed");
}

// Test utility functions
#[test]
fn test_concurrent_utility_functions() {
    // Test scramble_bytes
    let mut data = vec![0u8; 32];
    let original_data = data.clone();

    scramble_bytes(&mut data);
    assert_ne!(data, original_data, "Data should be scrambled");

    // Test wipe_bytes
    wipe_bytes(&mut data);
    assert!(
        data.iter().all(|&x| x == 0),
        "Data should be wiped to all zeroes"
    );
}

// Test buffer freeze/melt
#[test]
fn test_concurrent_buffer_freeze_melt() {
    let buffer = Buffer::new(32).expect("Buffer creation failed");

    // Initial state is mutable
    buffer
        .with_data_mut(|data| {
            data[0] = 0xAA;
            Ok(())
        })
        .expect("Writing to initial buffer failed");

    // Freeze the buffer
    buffer.freeze().expect("Freezing buffer failed");

    // Should be immutable now
    assert!(matches!(
        buffer.with_data_mut(|_| Ok(())),
        Err(MemguardError::ProtectionFailed(_))
    ));

    // Reading should still work
    buffer
        .with_data(|data| {
            assert_eq!(data[0], 0xAA);
            Ok(())
        })
        .expect("Reading from frozen buffer failed");

    // Melt the buffer
    buffer.melt().expect("Melting buffer failed");

    // Should be mutable again
    buffer
        .with_data_mut(|data| {
            data[0] = 0xBB;
            Ok(())
        })
        .expect("Writing to melted buffer failed");

    buffer
        .with_data(|data| {
            assert_eq!(data[0], 0xBB);
            Ok(())
        })
        .expect("Reading from melted buffer failed");

    buffer.destroy().expect("Buffer destruction failed");
}

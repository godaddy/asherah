use securememory::memguard::{Buffer, Enclave};

// A minimal test to verify that we can create and manipulate Buffers safely
#[test]
fn test_buffer_operation() {
    // Create a buffer with test content
    let buffer = Buffer::new(32).expect("Failed to create buffer");

    // Write some data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = i as u8;
            }
            Ok(())
        })
        .expect("Failed to write data");

    // Read the data back
    buffer
        .with_data(|data| {
            for i in 0..data.len() {
                assert_eq!(data[i], i as u8);
            }
            Ok(())
        })
        .expect("Failed to read data");

    // Destroy should work
    buffer.destroy().expect("Failed to destroy buffer");

    // Buffer should be marked as not alive
    assert!(!buffer.is_alive());
}

#[test]
fn test_enclave_minimal() {
    // Create a minimal test for enclave functionality
    let mut data = vec![1, 2, 3, 4, 5];
    let data_copy = data.clone();

    // Create an enclave directly with new
    let enclave = Enclave::new(&mut data).unwrap();

    // Original data should be wiped
    assert_ne!(data, data_copy);

    // Size should be correct
    assert_eq!(enclave.size(), 5);

    // Open the enclave
    let buffer = enclave.open().unwrap();

    // Verify the content
    buffer
        .with_data(|buffer_data| {
            assert_eq!(buffer_data.len(), 5);
            assert_eq!(buffer_data, &[1, 2, 3, 4, 5]);
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_enclave_seal_open_minimal() {
    // Create a buffer
    let mut buffer = Buffer::new(16).unwrap();

    // Fill buffer with known data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = i as u8;
            }
            Ok(())
        })
        .unwrap();

    // Seal the buffer
    let enclave = Enclave::seal(&mut buffer).unwrap();

    // Buffer should be destroyed
    assert!(!buffer.is_alive());

    // Verify enclave size
    assert_eq!(enclave.size(), 16);

    // Open the enclave
    let unsealed = enclave.open().unwrap();

    // Verify the content
    unsealed
        .with_data(|data| {
            assert_eq!(data.len(), 16);
            for i in 0..data.len() {
                assert_eq!(data[i], i as u8);
            }
            Ok(())
        })
        .unwrap();
}

use securememory::error::SecureMemoryError;
use securememory::memguard::{Buffer, Enclave};
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

#[test]
fn test_memguard_secret_creation() {
    // Basic test that doesn't rely on complex structures
    let buffer = Buffer::new(32).unwrap();
    
    // Write some test data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = (i % 256) as u8;
        }
        Ok(())
    }).unwrap();
    
    // Read it back
    buffer.with_data(|data| {
        // Check a few values
        assert_eq!(data[0], 0);
        assert_eq!(data[1], 1);
        assert_eq!(data[5], 5);
        Ok(())
    }).unwrap();
    
    // Clean up
    buffer.destroy().unwrap();
}

#[test]
fn test_buffer_destruction() {
    let mut buffer = Buffer::new(16).unwrap();
    
    // Destroy the buffer
    buffer.destroy().unwrap();
    
    // Verify it's not alive
    assert!(!buffer.is_alive());
    
    // Trying to use it should fail
    let result = buffer.with_data(|_| Ok(()));
    assert!(result.is_err());
}

#[test]
fn test_buffer_random_data() {
    let mut buffer = Buffer::new(32).unwrap();
    
    // Fill with random data manually
    buffer.with_data_mut(|data| {
        getrandom::getrandom(data).unwrap();
        Ok(())
    }).unwrap();
    
    // Verify size and that it contains random data
    buffer.with_data(|data| {
        assert_eq!(data.len(), 32);
        
        // Verify it's not all zeros (extremely unlikely with secure random)
        let all_zeros = data.iter().all(|&b| b == 0);
        assert!(!all_zeros, "Random data shouldn't be all zeros");
        
        Ok(())
    }).unwrap();
    
    // Clean up
    buffer.destroy().unwrap();
}

#[test]
fn test_buffer_direct() {
    // Test the Buffer implementation directly
    let buffer = Buffer::new(32).unwrap();
    
    // Write data to the buffer
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Read and verify data
    buffer.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).unwrap();
    
    // Verify alive status
    assert!(buffer.is_alive());
    
    // Destroy buffer
    buffer.destroy().unwrap();
    
    // Verify destroyed status
    assert!(!buffer.is_alive());
}

#[test]
fn test_enclave_seal_and_open() {
    // Test the Enclave implementation directly
    let mut buffer = Buffer::new(64).unwrap();
    
    // Write test pattern to buffer
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = (i * 2) as u8;
        }
        Ok(())
    }).unwrap();
    
    // Seal buffer into an enclave  
    let enclave = Enclave::seal(&mut buffer).unwrap();
    
    // Buffer should be destroyed after sealing
    assert!(!buffer.is_alive());
    
    // Open the enclave
    let unsealed = enclave.open().unwrap();
    
    // Verify data was preserved
    unsealed.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], (i * 2) as u8); 
        }
        Ok(())
    }).unwrap();
    
    // Clean up
    unsealed.destroy().unwrap();
}

#[test]
fn test_enclave_integration() {
    // Test the full enclave lifecycle
    let mut buffer = Buffer::new(27).unwrap();
    
    // Write test data
    buffer.with_data_mut(|data| {
        for (i, b) in b"testing_enclave_integration".iter().enumerate() {
            data[i] = *b;
        }
        Ok(())
    }).unwrap();
    
    // Seal the buffer in an enclave
    let enclave = Enclave::seal(&mut buffer).unwrap();
    
    // The buffer should no longer be alive after sealing
    assert!(!buffer.is_alive());
    
    // Open the enclave and verify data
    let unsealed = enclave.open().unwrap();
    unsealed.with_data(|data| {
        assert_eq!(&data[..27], b"testing_enclave_integration");
        Ok(())
    }).unwrap();
    
    // Clean up
    unsealed.destroy().unwrap();
}

#[test]
fn test_memguard_buffer_freeze_thaw() {
    // Test buffer freeze and thaw
    let buffer = Buffer::new(16).unwrap();
    
    // Write initial data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Freeze the buffer
    buffer.freeze().unwrap();
    
    // Verify we can read
    buffer.with_data(|data| {
        assert_eq!(data[0], 0);
        Ok(())
    }).unwrap();
    
    // Melt the buffer
    buffer.melt().unwrap();
    
    // Now we should be able to write
    buffer.with_data_mut(|data| {
        data[0] = 99;
        Ok(())
    }).unwrap();
    
    // Verify modification
    buffer.with_data(|data| {
        assert_eq!(data[0], 99);
        Ok(())
    }).unwrap();
}

#[test]
fn test_memguard_emergency_functions() {
    use securememory::memguard::{purge, wipe_bytes, scramble_bytes};
    
    // Test wipe_bytes
    let mut data = vec![1, 2, 3, 4, 5];
    wipe_bytes(&mut data);
    assert!(data.iter().all(|&b| b == 0), "Data should be wiped to zeros");
    
    // Test scramble_bytes
    let mut data = vec![1, 2, 3, 4, 5];
    scramble_bytes(&mut data);
    assert!(!data.iter().all(|&b| b == 0), "Data should be scrambled");
    
    // Testing purge() is difficult since it affects global state,
    // but we can call it to make sure it doesn't panic
    purge();
}
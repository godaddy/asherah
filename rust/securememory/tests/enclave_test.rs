use securememory::memguard::{Buffer, Enclave};
use std::panic;

#[test]
fn test_enclave_basic() {
    // Use a timeout to prevent the test from hanging indefinitely
    let test_result = panic::catch_unwind(|| {
        // Create a test buffer
        let buffer = Buffer::new(64).unwrap();
        
        // Prepare test data
        let mut test_data = vec![0u8; 64];
        for i in 0..test_data.len() {
            test_data[i] = (i % 256) as u8;
        }
        
        // Write data to buffer
        buffer.with_data_mut(|data| {
            assert_eq!(data.len(), test_data.len());
            data.copy_from_slice(&test_data);
            Ok(())
        }).unwrap();
        
        println!("Step 1: Created and filled buffer successfully");
        
        // Create enclave directly with Enclave::new
        let mut data_copy = test_data.clone();
        println!("Creating enclave with Enclave::new...");
        let enclave = Enclave::new(&mut data_copy).unwrap();
        println!("Step 2: Created enclave successfully");
        
        // Original data should be wiped
        assert_ne!(data_copy, test_data);
        
        // Open the enclave
        println!("Opening enclave...");
        let unsealed = enclave.open().unwrap();
        println!("Step 3: Opened enclave successfully");
        
        // Read the data from the unsealed buffer
        unsealed.with_data(|data| {
            // Data should match original
            assert_eq!(data.len(), test_data.len());
            assert_eq!(data, test_data);
            Ok(())
        }).unwrap();
        println!("Step 4: Verified unsealed data successfully");
        
        // Clean up
        buffer.destroy().unwrap();
        unsealed.destroy().unwrap();
        println!("Step 5: Cleaned up resources successfully");
    });
    
    if let Err(e) = test_result {
        panic!("Test failed: {:?}", e);
    }
}
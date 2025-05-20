// This is a minimal test that avoids using the Enclave directly
use securememory::memguard::Buffer;

#[test]
fn test_memory_operations() {
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
    
    // Read back and verify
    buffer.with_data(|data| {
        assert_eq!(data, test_data);
        Ok(())
    }).unwrap();
    
    // Test freeze and melt
    buffer.freeze().unwrap();
    
    // Should still be able to read
    buffer.with_data(|data| {
        assert_eq!(data, test_data);
        Ok(())
    }).unwrap();
    
    // Melt and modify
    buffer.melt().unwrap();
    
    buffer.with_data_mut(|data| {
        data[0] = 99;
        data[1] = 98;
        Ok(())
    }).unwrap();
    
    // Verify modification worked
    buffer.with_data(|data| {
        assert_eq!(data[0], 99);
        assert_eq!(data[1], 98);
        Ok(())
    }).unwrap();
    
    // Clean up
    buffer.destroy().unwrap();
    
    // Verify buffer is destroyed
    assert!(!buffer.is_alive());
}
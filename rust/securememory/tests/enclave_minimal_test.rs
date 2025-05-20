// This is an ultra-minimal test for enclave
// It bypasses the Enclave struct entirely and just tests the components it would use

use securememory::error::Result;
use securememory::memguard::Buffer;

#[test]
fn test_buffer_only() {
    // Create a buffer
    let buffer = Buffer::new(32).unwrap();
    
    // Fill with test data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Verify data
    buffer.with_data(|data| {
        assert_eq!(data[0], 0);
        assert_eq!(data[1], 1);
        Ok(())
    }).unwrap();
    
    // Clean up
    buffer.destroy().unwrap();
}

#[test]
fn test_buffer_minimal() -> Result<()> {
    // Create a minimal buffer
    let buffer = Buffer::new(64)?;
    
    // Use it directly
    buffer.with_data_mut(|data| {
        data[0] = 42;
        Ok(())
    })?;
    
    buffer.with_data(|data| {
        assert_eq!(data[0], 42);
        Ok(())
    })?;
    
    buffer.destroy()?;
    Ok(())
}

#[test]
fn test_buffer_freeze_melt() {
    // Create a buffer
    let buffer = Buffer::new(64).unwrap();
    
    // Fill it with data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Freeze it
    buffer.freeze().unwrap();
    
    // Should still be readable
    buffer.with_data(|data| {
        assert_eq!(data[0], 0);
        Ok(())
    }).unwrap();
    
    // Melt it
    buffer.melt().unwrap();
    
    // Should be writable again
    buffer.with_data_mut(|data| {
        data[0] = 99;
        Ok(())
    }).unwrap();
    
    // Clean up
    buffer.destroy().unwrap();
}

#[test]
fn test_memory_safety() -> Result<()> {
    // Create a buffer
    let buffer = Buffer::new(64)?;
    
    // Fill it with a pattern
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = (i % 256) as u8;
        }
        Ok(())
    })?;
    
    // Freeze it
    buffer.freeze()?;
    
    // Access with data should still work
    buffer.with_data(|data| {
        assert_eq!(data[0], 0);
        assert_eq!(data[1], 1);
        Ok(())
    })?;
    
    // Melt it
    buffer.melt()?;
    
    // Modify it
    buffer.with_data_mut(|data| {
        data[0] = 99;
        data[1] = 98;
        Ok(())
    })?;
    
    // Verify changes
    buffer.with_data(|data| {
        assert_eq!(data[0], 99);
        assert_eq!(data[1], 98);
        Ok(())
    })?;
    
    // Clean up
    buffer.destroy()?;
    
    Ok(())
}
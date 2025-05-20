use crate::Buffer;

#[test]
fn debug_test_buffer_creation() {
    println!("Starting buffer creation test");
    
    // Just try to create a simple buffer
    println!("About to call Buffer::new(16)");
    let result = Buffer::new(16);
    println!("Buffer::new returned");
    
    match result {
        Ok(buffer) => {
            println!("Buffer created successfully");
            // Try to destroy it
            println!("About to destroy buffer");
            if let Err(e) = buffer.destroy() {
                println!("Error destroying buffer: {:?}", e);
            } else {
                println!("Buffer destroyed successfully");
            }
        }
        Err(e) => {
            println!("Error creating buffer: {:?}", e);
        }
    }
    
    println!("Test completed");
}
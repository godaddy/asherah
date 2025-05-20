use crate::Buffer;

#[test]
fn test_buffer_create_simple() {
    // Test just Buffer creation without any debug prints
    match Buffer::new(16) {
        Ok(buffer) => {
            println!("Buffer created successfully");
            
            // Try to destroy it
            match buffer.destroy() {
                Ok(()) => println!("Buffer destroyed successfully"),
                Err(e) => println!("Error destroying buffer: {:?}", e),
            }
        }
        Err(e) => {
            println!("Error creating buffer: {:?}", e);
        }
    }
}
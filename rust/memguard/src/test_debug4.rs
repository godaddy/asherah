use crate::Buffer;

#[test]
fn debug_test_real_issues() {
    println!("Starting test");
    
    // Let's carefully trace through what happens when creating a buffer
    println!("Step 1: About to create buffer");
    
    // First try with a smaller size
    match Buffer::new(1) {
        Ok(_buffer) => {
            println!("Successfully created buffer");
        }
        Err(e) => {
            println!("Failed to create buffer: {:?}", e);
        }
    }
    
    println!("Test completed");
}
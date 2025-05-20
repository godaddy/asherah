use securememory::protected_memory::factory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

#[test]
fn test_simple_protection() {
    let factory = DefaultSecretFactory::new();
    
    // Create a secret with some data
    let mut data = b"test data".to_vec();
    println!("Creating secret with data: {:?}", data);
    
    match factory.new(&mut data) {
        Ok(secret) => {
            println!("Secret created successfully");
            
            // Try to access it
            match secret.with_bytes(|bytes| {
                println!("Accessed bytes: {:?}", bytes);
                Ok(())
            }) {
                Ok(_) => println!("Access successful"),
                Err(e) => println!("Access failed: {:?}", e),
            }
            
            // Try to close it
            match secret.close() {
                Ok(_) => println!("Close successful"),
                Err(e) => println!("Close failed: {:?}", e),
            }
        }
        Err(e) => {
            println!("Failed to create secret: {:?}", e);
            panic!("Test failed");
        }
    }
}

#[test]
fn test_page_alignment() {
    use memcall;
    
    let page_size = memcall::page_size();
    println!("Page size: {}", page_size);
    
    // Create a simple vector
    let mut vec = vec![0u8; 100];
    println!("Vector address: {:p}", vec.as_ptr());
    println!("Vector alignment: {} (page aligned: {})", 
             vec.as_ptr() as usize % page_size,
             vec.as_ptr() as usize % page_size == 0);
    
    // Try to protect it
    match memcall::protect(&mut vec, memcall::MemoryProtection::NoAccess) {
        Ok(_) => println!("Protection successful"),
        Err(e) => println!("Protection failed: {:?}", e),
    }
}
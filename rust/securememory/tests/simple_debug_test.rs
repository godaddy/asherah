use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretFactory, SecretExtensions};

#[test]
fn test_simple_creation_debug() {
    println!("Starting test");
    
    let factory = DefaultSecretFactory::new();
    println!("Factory created");
    
    let mut data = b"test".to_vec();
    println!("Data: {:?}", data);
    
    // Create the secret
    let secret = factory.new(&mut data).unwrap();
    println!("Secret created");
    
    // Access the data
    secret.with_bytes(|bytes| {
        println!("Inside with_bytes");
        println!("Bytes: {:?}", bytes);
        assert_eq!(bytes, b"test");
        Ok(())
    }).unwrap();
    
    println!("Test complete, letting drop happen");
    // Drop happens automatically
}

#[test]
fn test_minimal_allocation() {
    println!("Starting minimal allocation test");
    
    // Test allocating page-aligned memory directly
    unsafe {
        let page_size = memcall::page_size();
        println!("Page size: {}", page_size);
        
        let size = 4096;
        let aligned_size = ((size + page_size - 1) / page_size) * page_size;
        println!("Requesting size: {}, aligned size: {}", size, aligned_size);
        
        match memcall::allocate_aligned(aligned_size, page_size) {
            Ok(ptr) => {
                println!("Successfully allocated memory at {:p}", ptr);
                
                // Try to free it
                let result = memcall::free_aligned(ptr, aligned_size);
                println!("Free result: {:?}", result);
            }
            Err(e) => {
                println!("Failed to allocate memory: {:?}", e);
            }
        }
    }
    
    println!("Minimal allocation test complete");
}
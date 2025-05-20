use securememory::protected_memory::factory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::sync::Arc;
use std::thread;
use std::time::Duration;

#[test]
fn test_debug_sigbus() {
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"test data".to_vec()).unwrap();
    let secret = Arc::new(secret);
    
    println!("Main: Created secret");
    
    // Create a reader thread
    let secret_reader = Arc::clone(&secret);
    let reader = thread::spawn(move || {
        println!("Reader: Starting");
        for i in 0..10 {
            println!("Reader: Attempt {}", i);
            match secret_reader.with_bytes(|bytes| {
                println!("Reader: Got bytes: {:?}", bytes);
                thread::sleep(Duration::from_millis(10));
                Ok(())
            }) {
                Ok(_) => println!("Reader: Success"),
                Err(e) => {
                    println!("Reader: Error: {:?}", e);
                    break;
                }
            }
        }
        println!("Reader: Exiting");
    });
    
    // Give reader time to start
    thread::sleep(Duration::from_millis(50));
    
    // Close the secret
    println!("Main: Closing secret");
    secret.close().unwrap();
    println!("Main: Secret closed");
    
    // Wait for reader
    reader.join().unwrap();
    println!("Main: Test complete");
}

#[test]
fn test_minimal_case() {
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"test".to_vec()).unwrap();
    
    // Simply close it
    secret.close().unwrap();
    
    // Try to access after close
    match secret.with_bytes(|_| Ok(())) {
        Ok(_) => panic!("Should have failed"),
        Err(_) => println!("Correctly failed after close"),
    }
}
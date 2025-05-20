use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use securememory::test_utils::TestGuard;
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::Duration;

#[test]
fn test_simple_single_reader_close() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    println!("=== Test single reader close ===");
    
    let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
    let barrier = Arc::new(Barrier::new(2));
    
    let secret_reader = Arc::clone(&secret);
    let barrier_reader = Arc::clone(&barrier);
    
    let reader = thread::spawn(move || {
        barrier_reader.wait();
        println!("Reader starting");
        
        let mut count = 0;
        loop {
            match secret_reader.with_bytes(|_| Ok(())) {
                Ok(_) => {
                    count += 1;
                    if count % 100 == 0 {
                        println!("Reader accessed {} times", count);
                        // Add a small sleep to let the closer run
                        thread::sleep(Duration::from_micros(10));
                    }
                }
                Err(_) => {
                    println!("Reader saw closed secret after {} accesses", count);
                    break;
                }
            }
        }
        println!("Reader exiting");
    });
    
    barrier.wait();
    
    // Give reader a chance to start
    thread::sleep(Duration::from_millis(10));
    
    println!("Main thread attempting to close...");
    secret.close().unwrap();
    println!("Main thread closed secret");
    
    // Wait for reader to finish
    reader.join().unwrap();
    println!("Test complete");
}

#[test]
fn test_direct_close() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    println!("=== Test direct close ===");
    
    let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();
    
    // Access it once
    secret.with_bytes(|bytes| {
        println!("Accessed bytes: {:?}", bytes);
        Ok(())
    }).unwrap();
    
    println!("Closing secret...");
    secret.close().unwrap();
    println!("Secret closed");
    
    // Try to access after close
    match secret.with_bytes(|_| Ok(())) {
        Ok(_) => panic!("Should not be able to access closed secret"),
        Err(e) => println!("Expected error: {:?}", e),
    }
}
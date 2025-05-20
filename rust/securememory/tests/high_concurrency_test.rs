use securememory::protected_memory::factory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::Duration;
use securememory::test_utils::TestGuard;

#[test]
fn test_high_concurrency() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"test data for race condition".to_vec()).unwrap();
    let secret = Arc::new(secret);
    
    const NUM_THREADS: usize = 20;
    let barrier = Arc::new(Barrier::new(NUM_THREADS + 1));
    
    let mut handles = Vec::new();
    
    // Create reader threads (note: creates NUM_THREADS threads total)
    for i in 0..NUM_THREADS-1 {  // Reserve one thread for the closer
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        
        let handle = thread::spawn(move || {
            barrier_clone.wait();
            
            for j in 0..100 {
                match secret_clone.with_bytes(|bytes| {
                    assert_eq!(bytes.len(), 28);
                    Ok(())
                }) {
                    Ok(_) => {},
                    Err(_) => {
                        // Expected when secret is closed
                        break;
                    }
                }
                if j % 10 == 0 {
                    thread::yield_now();
                }
            }
        });
        
        handles.push(handle);
    }
    
    // Create a closer thread
    let secret_clone = Arc::clone(&secret);
    let barrier_clone = Arc::clone(&barrier);
    
    let closer = thread::spawn(move || {
        barrier_clone.wait();
        thread::sleep(Duration::from_millis(10));
        secret_clone.close().unwrap();
    });
    
    handles.push(closer);
    
    // Release all threads
    barrier.wait();
    
    // Wait for all threads to complete
    for handle in handles {
        handle.join().unwrap();
    }
}

#[test]
fn test_rapid_access_close() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    for _ in 0..100 {
        let factory = DefaultSecretFactory::new();
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        let secret = Arc::new(secret);
        
        let secret_reader = Arc::clone(&secret);
        let reader = thread::spawn(move || {
            for _ in 0..100 {
                match secret_reader.with_bytes(|_| Ok(())) {
                    Ok(_) => {},
                    Err(_) => break,
                }
            }
        });
        
        // Close almost immediately
        thread::yield_now();
        secret.close().unwrap();
        
        reader.join().unwrap();
    }
}
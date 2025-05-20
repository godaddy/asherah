use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretFactory, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::sync::atomic::{AtomicBool, Ordering};
use securememory::error::SecureMemoryError;

#[test]
fn test_exact_failing_pattern() {
    let factory = DefaultSecretFactory::new();
    
    for iteration in 0..2 {
        println!("Iteration {}", iteration);
        
        let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
        let copy_bytes = orig.clone();
        
        let secret = Arc::new(factory.new(&mut orig).unwrap());
        let closed = Arc::new(AtomicBool::new(false));
        
        // Create a barrier to synchronize all threads
        let barrier = Arc::new(Barrier::new(10)); // 9 readers + 1 main
        
        let mut handles = Vec::with_capacity(9);
        
        // Create reader threads
        for i in 0..9 {
            let secret_clone = Arc::clone(&secret);
            let barrier_clone = Arc::clone(&barrier);
            let closed_clone = Arc::clone(&closed);
            let copy_bytes_clone = copy_bytes.clone();
            
            let handle = thread::spawn(move || {
                barrier_clone.wait(); // Wait for all threads to be ready
                
                // Keep trying to read the secret until the test signals it's closed
                while !closed_clone.load(Ordering::SeqCst) {
                    let result = secret_clone.with_bytes(|bytes| {
                        if bytes.len() == copy_bytes_clone.len() {
                            assert_eq!(bytes, copy_bytes_clone.as_slice());
                        }
                        Ok(())
                    });
                    
                    // If the secret is closed, we expect an error
                    if let Err(e) = result {
                        match e {
                            SecureMemoryError::SecretClosed => {
                                println!("Thread {} got SecretClosed", i);
                                closed_clone.store(true, Ordering::SeqCst);
                                break;
                            },
                            _ => panic!("Thread {} unexpected error: {:?}", i, e),
                        }
                    }
                    
                    // Small yield to create more interleaving opportunities
                    thread::yield_now();
                }
                println!("Thread {} done", i);
            });
            
            handles.push(handle);
        }
        
        // Release all threads
        barrier.wait();
        
        // Let readers run for a bit
        thread::sleep(std::time::Duration::from_millis(5));
        
        // The original test just sets a flag, which is wrong
        // What if we actually close the secret?
        println!("Main thread closing secret");
        secret.close().unwrap();
        
        // Signal to threads that secret is closed
        closed.store(true, Ordering::SeqCst);
        
        // Wait for all threads to complete
        for (i, handle) in handles.into_iter().enumerate() {
            println!("Waiting for thread {}", i);
            handle.join().unwrap();
        }
        
        println!("Iteration {} complete", iteration);
    }
}

#[test]
fn test_many_readers_stress() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    let barrier = Arc::new(Barrier::new(20));
    let done = Arc::new(AtomicBool::new(false));
    
    let mut handles = vec![];
    
    for i in 0..20 {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        let done_clone = Arc::clone(&done);
        
        let handle = thread::spawn(move || {
            barrier_clone.wait();
            
            let mut count = 0;
            while !done_clone.load(Ordering::Relaxed) {
                match secret_clone.with_bytes(|bytes| {
                    assert_eq!(bytes.len(), 32);
                    Ok(())
                }) {
                    Ok(_) => count += 1,
                    Err(SecureMemoryError::SecretClosed) => break,
                    Err(e) => panic!("Unexpected error: {:?}", e),
                }
                
                if count % 1000 == 0 {
                    thread::yield_now();
                }
            }
            
            println!("Thread {} completed {} iterations", i, count);
        });
        
        handles.push(handle);
    }
    
    // Let threads run
    thread::sleep(std::time::Duration::from_millis(100));
    
    // Close the secret
    println!("Closing secret");
    secret.close().unwrap();
    
    // Signal done
    done.store(true, Ordering::Relaxed);
    
    for handle in handles {
        handle.join().unwrap();
    }
    
    println!("Many readers stress test passed");
}
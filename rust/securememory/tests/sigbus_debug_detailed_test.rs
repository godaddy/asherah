use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretFactory, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};

#[test]
fn test_sigbus_with_counters() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    let access_count = Arc::new(AtomicU32::new(0));
    let error_count = Arc::new(AtomicU32::new(0));
    let closed_seen = Arc::new(AtomicU32::new(0));
    let done = Arc::new(AtomicBool::new(false));
    
    let mut handles = vec![];
    
    // Start threads
    for i in 0..9 {
        let secret_clone = Arc::clone(&secret);
        let access_count_clone = Arc::clone(&access_count);
        let error_count_clone = Arc::clone(&error_count);
        let closed_seen_clone = Arc::clone(&closed_seen);
        let done_clone = Arc::clone(&done);
        
        let handle = thread::spawn(move || {
            println!("Thread {} starting", i);
            
            while !done_clone.load(Ordering::Relaxed) {
                match secret_clone.with_bytes(|bytes| {
                    access_count_clone.fetch_add(1, Ordering::Relaxed);
                    assert_eq!(bytes.len(), 32);
                    Ok(())
                }) {
                    Ok(_) => {
                        // Success
                    }
                    Err(securememory::error::SecureMemoryError::SecretClosed) => {
                        closed_seen_clone.fetch_add(1, Ordering::Relaxed);
                        println!("Thread {} saw closed", i);
                        break;
                    }
                    Err(e) => {
                        error_count_clone.fetch_add(1, Ordering::Relaxed);
                        println!("Thread {} error: {:?}", i, e);
                        break;
                    }
                }
                
                thread::yield_now();
            }
            
            println!("Thread {} done", i);
        });
        
        handles.push(handle);
    }
    
    // Let them run
    thread::sleep(std::time::Duration::from_millis(50));
    
    // Close the secret
    println!("Main thread closing secret");
    secret.close().unwrap();
    
    // Signal done
    done.store(true, Ordering::Relaxed);
    
    // Join all threads
    for handle in handles {
        handle.join().unwrap();
    }
    
    let accesses = access_count.load(Ordering::Relaxed);
    let errors = error_count.load(Ordering::Relaxed);
    let closed = closed_seen.load(Ordering::Relaxed);
    
    println!("Total accesses: {}", accesses);
    println!("Errors: {}", errors);
    println!("Closed seen: {}", closed);
    
    assert_eq!(errors, 0);
    assert!(closed > 0);
}

#[test]
fn test_sigbus_with_barrier_start() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    let barrier = Arc::new(Barrier::new(10)); // 9 threads + main
    
    let mut handles = vec![];
    
    // Start all threads at once
    for i in 0..9 {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        
        let handle = thread::spawn(move || {
            println!("Thread {} waiting at barrier", i);
            barrier_clone.wait();
            println!("Thread {} past barrier", i);
            
            // Try one access
            match secret_clone.with_bytes(|bytes| {
                println!("Thread {} accessing bytes", i);
                assert_eq!(bytes.len(), 32);
                Ok(())
            }) {
                Ok(_) => println!("Thread {} success", i),
                Err(e) => println!("Thread {} error: {:?}", i, e),
            }
        });
        
        handles.push(handle);
    }
    
    // Wait for all threads to be ready
    println!("Main waiting at barrier");
    barrier.wait();
    println!("Main past barrier");
    
    // Give threads a moment to start accessing
    thread::sleep(std::time::Duration::from_millis(10));
    
    // Now close
    println!("Main closing secret");
    match secret.close() {
        Ok(_) => println!("Main close success"),
        Err(e) => println!("Main close error: {:?}", e),
    }
    
    // Join all
    for (i, handle) in handles.into_iter().enumerate() {
        println!("Joining thread {}", i);
        handle.join().unwrap();
    }
    
    println!("Barrier test complete");
}
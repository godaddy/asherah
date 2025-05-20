use std::sync::{Arc, atomic::{AtomicBool, Ordering}};
use std::thread;
use std::time::Duration;
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

/// Simplest possible test to isolate the SIGBUS issue
#[test]
fn test_sigbus_analysis() {
    let factory = DefaultSecretFactory::new();
    
    // Simple test: one thread reads while another closes
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();
    
    let secret = Arc::new(factory.new(&mut orig).unwrap());
    let closed = Arc::new(AtomicBool::new(false));
    
    // Create reader thread
    let secret_clone = Arc::clone(&secret);
    let copy_bytes_clone = copy_bytes.clone();
    let closed_clone = Arc::clone(&closed);
    
    let reader = thread::spawn(move || {
        // Keep reading until marked as closed
        while !closed_clone.load(Ordering::SeqCst) {
            match secret_clone.with_bytes(|bytes| {
                // Just access the bytes to trigger any potential issues
                if bytes.len() > 0 {
                    let _ = bytes[0];
                    assert_eq!(bytes, copy_bytes_clone.as_slice());
                }
                Ok(())
            }) {
                Ok(()) => {
                    thread::yield_now();
                },
                Err(_) => {
                    // Secret closed, exit
                    break;
                }
            }
        }
        println!("Reader thread exiting");
    });
    
    // Give reader a chance to start
    thread::sleep(Duration::from_millis(10));
    
    // Close the secret from main thread
    println!("Closing secret from main thread");
    secret.close().unwrap();
    closed.store(true, Ordering::SeqCst);
    
    // Wait for reader to finish
    reader.join().unwrap();
    println!("Test completed successfully");
}

/// Test to see if the issue happens with multiple readers
#[test]
fn test_sigbus_multiple_readers() {
    let factory = DefaultSecretFactory::new();
    
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();
    
    let secret = Arc::new(factory.new(&mut orig).unwrap());
    let closed = Arc::new(AtomicBool::new(false));
    
    // Create multiple reader threads
    let mut handles = vec![];
    for i in 0..3 {
        let secret_clone = Arc::clone(&secret);
        let copy_bytes_clone = copy_bytes.clone();
        let closed_clone = Arc::clone(&closed);
        
        let reader = thread::spawn(move || {
            println!("Reader {} starting", i);
            let mut read_count = 0;
            
            while !closed_clone.load(Ordering::SeqCst) {
                match secret_clone.with_bytes(|bytes| {
                    if bytes.len() > 0 {
                        let _ = bytes[0];
                        assert_eq!(bytes, copy_bytes_clone.as_slice());
                    }
                    Ok(())
                }) {
                    Ok(()) => {
                        read_count += 1;
                        thread::yield_now();
                    },
                    Err(_) => {
                        println!("Reader {} got error after {} reads", i, read_count);
                        break;
                    }
                }
            }
            println!("Reader {} exiting after {} reads", i, read_count);
        });
        handles.push(reader);
    }
    
    // Give readers a chance to start
    thread::sleep(Duration::from_millis(20));
    
    // Close the secret
    println!("Closing secret from main thread");
    secret.close().unwrap();
    closed.store(true, Ordering::SeqCst);
    
    // Wait for all readers
    for handle in handles {
        handle.join().unwrap();
    }
    println!("All readers finished");
}
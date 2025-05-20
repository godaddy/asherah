use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretFactory, SecretExtensions};
use std::sync::Arc;
use std::thread;
use std::sync::atomic::{AtomicBool, Ordering};

#[test]
fn test_sigbus_minimal() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();
    
    // Just access it once
    secret.with_bytes(|bytes| {
        println!("Initial access: {:?}", bytes);
        Ok(())
    }).unwrap();
    
    drop(secret);
    println!("Test passed");
}

#[test]
fn test_sigbus_with_arc() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    // Clone and access
    let secret_clone = Arc::clone(&secret);
    secret_clone.with_bytes(|bytes| {
        println!("Access through Arc: {:?}", bytes);
        Ok(())
    }).unwrap();
    
    drop(secret_clone);
    drop(secret);
    println!("Arc test passed");
}

#[test]
fn test_sigbus_concurrent_minimal() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    let done = Arc::new(AtomicBool::new(false));
    
    let secret_reader = Arc::clone(&secret);
    let done_reader = Arc::clone(&done);
    
    let reader = thread::spawn(move || {
        while !done_reader.load(Ordering::Relaxed) {
            match secret_reader.with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            }) {
                Ok(_) => {},
                Err(e) => {
                    println!("Reader got error: {}", e);
                    break;
                }
            }
            thread::yield_now();
        }
        println!("Reader thread done");
    });
    
    // Let the reader run a bit
    thread::sleep(std::time::Duration::from_millis(10));
    
    // Signal done
    done.store(true, Ordering::Relaxed);
    
    // Drop our reference
    drop(secret);
    
    reader.join().unwrap();
    println!("Concurrent minimal test passed");
}

#[test]
fn test_sigbus_drop_while_reading() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    let secret_reader = Arc::clone(&secret);
    
    let reader = thread::spawn(move || {
        for i in 0..10 {
            match secret_reader.with_bytes(|bytes| {
                println!("Reader iteration {}: {:?}", i, bytes);
                // Simulate some work
                thread::sleep(std::time::Duration::from_millis(5));
                Ok(())
            }) {
                Ok(_) => {},
                Err(e) => {
                    println!("Reader got error: {}", e);
                    break;
                }
            }
        }
        println!("Reader done");
    });
    
    // Give reader time to start
    thread::sleep(std::time::Duration::from_millis(20));
    
    // Drop our reference while reader might be active
    println!("Main thread dropping secret");
    drop(secret);
    
    reader.join().unwrap();
    println!("Drop while reading test passed");
}
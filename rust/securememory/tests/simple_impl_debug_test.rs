use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};

#[test]
fn test_simple_create_close() {
    println!("=== Testing simple create/close ===");
    
    let result = std::panic::catch_unwind(|| {
        println!("Creating secret...");
        let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();
        println!("Secret created");
        
        println!("Closing secret...");
        secret.close().unwrap();
        println!("Secret closed");
    });
    
    match result {
        Ok(_) => println!("Test passed"),
        Err(e) => {
            eprintln!("Test failed: {:?}", e);
            panic!("Simple create/close failed");
        }
    }
}

#[test]
fn test_simple_create_access_close() {
    println!("=== Testing create/access/close ===");
    
    let result = std::panic::catch_unwind(|| {
        println!("Creating secret...");
        let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();
        println!("Secret created");
        
        println!("Accessing secret...");
        secret.with_bytes(|bytes| {
            println!("Got bytes: {:?}", bytes);
            assert_eq!(bytes, b"test data");
            Ok(())
        }).unwrap();
        println!("Access complete");
        
        println!("Closing secret...");
        secret.close().unwrap();
        println!("Secret closed");
    });
    
    match result {
        Ok(_) => println!("Test passed"),
        Err(e) => {
            eprintln!("Test failed: {:?}", e);
            panic!("Simple create/access/close failed");
        }
    }
}

#[test]
fn test_simple_concurrent_access() {
    use std::sync::{Arc, Barrier};
    use std::thread;
    
    println!("=== Testing concurrent access ===");
    
    let result = std::panic::catch_unwind(|| {
        let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
        let barrier = Arc::new(Barrier::new(3));
        
        let mut handles = Vec::new();
        
        for i in 0..2 {
            let secret_clone = Arc::clone(&secret);
            let barrier_clone = Arc::clone(&barrier);
            
            let handle = thread::spawn(move || {
                barrier_clone.wait();
                
                match secret_clone.with_bytes(|bytes| {
                    println!("Thread {} accessed: {:?}", i, bytes);
                    assert_eq!(bytes, b"test data");
                    Ok(())
                }) {
                    Ok(_) => println!("Thread {} succeeded", i),
                    Err(e) => println!("Thread {} failed: {:?}", i, e),
                }
            });
            
            handles.push(handle);
        }
        
        barrier.wait();
        
        for handle in handles {
            handle.join().unwrap();
        }
        
        println!("Closing secret...");
        secret.close().unwrap();
        println!("Secret closed");
    });
    
    match result {
        Ok(_) => println!("Test passed"),
        Err(e) => {
            eprintln!("Test failed: {:?}", e);
            panic!("Simple concurrent access failed");
        }
    }
}

#[test]
fn test_simple_close_during_access() {
    use std::sync::{Arc, Barrier};
    use std::thread;
    use std::time::Duration;
    
    println!("=== Testing close during access ===");
    
    let result = std::panic::catch_unwind(|| {
        let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
        let barrier = Arc::new(Barrier::new(2));
        
        let secret_reader = Arc::clone(&secret);
        let barrier_reader = Arc::clone(&barrier);
        
        let reader = thread::spawn(move || {
            barrier_reader.wait();
            println!("Reader started");
            
            for i in 0..10 {
                match secret_reader.with_bytes(|_| Ok(())) {
                    Ok(_) => {
                        println!("Access {} succeeded", i);
                        thread::sleep(Duration::from_millis(10));
                    }
                    Err(_) => {
                        println!("Access {} failed - secret closed", i);
                        break;
                    }
                }
            }
            println!("Reader exiting");
        });
        
        barrier.wait();
        
        thread::sleep(Duration::from_millis(50));
        
        println!("Main thread closing secret...");
        secret.close().unwrap();
        println!("Main thread closed secret");
        
        reader.join().unwrap();
    });
    
    match result {
        Ok(_) => println!("Test passed"),
        Err(e) => {
            eprintln!("Test failed: {:?}", e);
            panic!("Simple close during access failed");
        }
    }
}

#[test]
fn test_memory_alignment() {
    println!("=== Testing memory alignment issue ===");
    
    // Test that our memory allocation is page-aligned
    let page_size = memcall::page_size();
    println!("System page size: {}", page_size);
    
    // Create a small secret
    let secret = ProtectedMemorySecretSimple::new(b"test").unwrap();
    println!("Secret created");
    
    // Access it
    secret.with_bytes(|bytes| {
        println!("Bytes: {:?}", bytes);
        assert_eq!(bytes, b"test");
        Ok(())
    }).unwrap();
    
    // Close it
    secret.close().unwrap();
    println!("Secret closed successfully");
}
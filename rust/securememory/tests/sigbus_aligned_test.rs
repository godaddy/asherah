use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretFactory, Secret, SecretExtensions};
use std::thread;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Mutex;

#[test]
fn test_aligned_simple() {
    println!("Test 1: Creating simple secret");
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"test".to_vec()).unwrap();
    println!("Test 1: Created secret");
    
    // Test normal access
    secret.with_bytes(|bytes| {
        println!("Test 1: In with_bytes, bytes len: {}", bytes.len());
        assert_eq!(bytes, b"test");
        Ok(())
    }).unwrap();
    
    println!("Test 1: Closing secret");
    secret.close().unwrap();
    println!("Test 1: Closed secret");
}

#[test]
fn test_aligned_concurrent_no_close() {
    println!("Test 2: Testing concurrent access without close");
    let factory = DefaultSecretFactory::new();
    let secret = Arc::new(factory.new(&mut b"test".to_vec()).unwrap());
    let running = Arc::new(AtomicBool::new(true));
    
    // Spawn multiple reader threads
    let mut handles = vec![];
    for i in 0..5 {
        let secret_clone = Arc::clone(&secret);
        let running_clone = Arc::clone(&running);
        let handle = thread::spawn(move || {
            println!("Test 2: Thread {} starting", i);
            while running_clone.load(Ordering::Acquire) {
                if let Ok(()) = secret_clone.with_bytes(|bytes| {
                    assert_eq!(bytes, b"test");
                    Ok(())
                }) {
                    thread::yield_now();
                }
            }
            println!("Test 2: Thread {} exiting", i);
        });
        handles.push(handle);
    }
    
    // Let threads run
    thread::sleep(std::time::Duration::from_millis(100));
    
    // Stop threads
    running.store(false, Ordering::Release);
    
    // Wait for threads
    for handle in handles {
        handle.join().unwrap();
    }
    
    println!("Test 2: All threads finished");
}

#[test]
fn test_aligned_close_after_threads() {
    println!("Test 3: Testing close after threads finish");
    let factory = DefaultSecretFactory::new();
    let secret = Arc::new(factory.new(&mut b"test".to_vec()).unwrap());
    let running = Arc::new(AtomicBool::new(true));
    
    // Spawn multiple reader threads
    let mut handles = vec![];
    for i in 0..5 {
        let secret_clone = Arc::clone(&secret);
        let running_clone = Arc::clone(&running);
        let handle = thread::spawn(move || {
            println!("Test 3: Thread {} starting", i);
            while running_clone.load(Ordering::Acquire) {
                if let Ok(()) = secret_clone.with_bytes(|bytes| {
                    assert_eq!(bytes, b"test");
                    Ok(())
                }) {
                    thread::yield_now();
                }
            }
            println!("Test 3: Thread {} exiting", i);
        });
        handles.push(handle);
    }
    
    // Let threads run
    thread::sleep(std::time::Duration::from_millis(100));
    
    // Stop threads
    running.store(false, Ordering::Release);
    
    // Wait for threads
    for handle in handles {
        handle.join().unwrap();
    }
    
    println!("Test 3: All threads finished, now closing");
    
    // Now close
    secret.close().unwrap();
    
    println!("Test 3: Closed successfully");
}

#[test]
fn test_aligned_single_concurrent_close() {
    println!("Test 4: Testing single concurrent close");
    let factory = DefaultSecretFactory::new();
    let secret = Arc::new(factory.new(&mut b"test".to_vec()).unwrap());
    
    // Spawn a reader thread
    let secret_clone = Arc::clone(&secret);
    let handle = thread::spawn(move || {
        println!("Test 4: Reader thread starting");
        for _ in 0..10 {
            if let Ok(()) = secret_clone.with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            }) {
                thread::sleep(std::time::Duration::from_millis(10));
            } else {
                println!("Test 4: Reader got error (expected after close)");
                break;
            }
        }
        println!("Test 4: Reader thread exiting");
    });
    
    // Give reader time to start
    thread::sleep(std::time::Duration::from_millis(50));
    
    // Close while reader is running
    println!("Test 4: Main thread closing secret");
    secret.close().unwrap();
    println!("Test 4: Main thread closed secret");
    
    // Wait for reader
    handle.join().unwrap();
    
    println!("Test 4: Test complete");
}

#[test]
fn test_aligned_trace_cleanup() {
    println!("Test 5: Tracing cleanup operations");
    
    let results = Arc::new(Mutex::new(Vec::new()));
    let results_clone = Arc::clone(&results);
    
    // Create and immediately drop/close
    {
        println!("Test 5: Creating secret");
        let factory = DefaultSecretFactory::new();
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        results_clone.lock().unwrap().push("Created");
        
        println!("Test 5: Accessing secret");
        secret.with_bytes(|bytes| {
            assert_eq!(bytes, b"test");
            results_clone.lock().unwrap().push("Accessed");
            Ok(())
        }).unwrap();
        
        println!("Test 5: Closing secret");
        secret.close().unwrap();
        results_clone.lock().unwrap().push("Closed");
        
        println!("Test 5: Secret going out of scope");
    }
    
    results_clone.lock().unwrap().push("Dropped");
    
    println!("Test 5: Results: {:?}", results.lock().unwrap());
}
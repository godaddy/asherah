use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretFactory, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::sync::atomic::{AtomicBool, Ordering};

#[test]
fn test_two_threads_no_barrier() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    
    let secret1 = Arc::clone(&secret);
    let secret2 = Arc::clone(&secret);
    
    let t1 = thread::spawn(move || {
        for i in 0..5 {
            secret1.with_bytes(|bytes| {
                println!("Thread 1, iteration {}: {:?}", i, bytes);
                Ok(())
            }).unwrap();
        }
    });
    
    let t2 = thread::spawn(move || {
        for i in 0..5 {
            secret2.with_bytes(|bytes| {
                println!("Thread 2, iteration {}: {:?}", i, bytes);
                Ok(())
            }).unwrap();
        }
    });
    
    t1.join().unwrap();
    t2.join().unwrap();
    println!("Two threads test passed");
}

#[test]
fn test_two_threads_with_barrier() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    let barrier = Arc::new(Barrier::new(2));
    
    let secret1 = Arc::clone(&secret);
    let secret2 = Arc::clone(&secret);
    let barrier1 = Arc::clone(&barrier);
    let barrier2 = Arc::clone(&barrier);
    
    let t1 = thread::spawn(move || {
        barrier1.wait();
        for i in 0..5 {
            secret1.with_bytes(|bytes| {
                println!("Thread 1, iteration {}: {:?}", i, bytes);
                Ok(())
            }).unwrap();
        }
    });
    
    let t2 = thread::spawn(move || {
        barrier2.wait();
        for i in 0..5 {
            secret2.with_bytes(|bytes| {
                println!("Thread 2, iteration {}: {:?}", i, bytes);
                Ok(())
            }).unwrap();
        }
    });
    
    t1.join().unwrap();
    t2.join().unwrap();
    println!("Two threads with barrier test passed");
}

#[test]
fn test_many_threads_with_barrier() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    let barrier = Arc::new(Barrier::new(10));
    
    let mut handles = vec![];
    
    for i in 0..10 {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        
        let handle = thread::spawn(move || {
            barrier_clone.wait();
            for j in 0..5 {
                match secret_clone.with_bytes(|bytes| {
                    println!("Thread {}, iteration {}: len={}", i, j, bytes.len());
                    assert_eq!(bytes.len(), 32);
                    Ok(())
                }) {
                    Ok(_) => {},
                    Err(e) => {
                        println!("Thread {} got error: {}", i, e);
                        break;
                    }
                }
            }
        });
        
        handles.push(handle);
    }
    
    for handle in handles {
        handle.join().unwrap();
    }
    println!("Many threads with barrier test passed");
}

#[test]
fn test_with_early_drop() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());
    let done = Arc::new(AtomicBool::new(false));
    
    let mut handles = vec![];
    
    for i in 0..5 {
        let secret_clone = Arc::clone(&secret);
        let done_clone = Arc::clone(&done);
        
        let handle = thread::spawn(move || {
            while !done_clone.load(Ordering::Relaxed) {
                match secret_clone.with_bytes(|bytes| {
                    println!("Thread {}: {:?}", i, bytes);
                    thread::sleep(std::time::Duration::from_millis(10));
                    Ok(())
                }) {
                    Ok(_) => {},
                    Err(e) => {
                        println!("Thread {} got error: {}", i, e);
                        break;
                    }
                }
            }
            println!("Thread {} exiting", i);
        });
        handles.push(handle);
    }
    
    // Let threads run
    thread::sleep(std::time::Duration::from_millis(50));
    
    // Drop our reference
    println!("Main dropping secret");
    drop(secret);
    
    // Let threads see the drop
    thread::sleep(std::time::Duration::from_millis(50));
    
    // Signal done
    done.store(true, Ordering::Relaxed);
    
    for handle in handles {
        handle.join().unwrap();
    }
    
    println!("Early drop test passed");
}
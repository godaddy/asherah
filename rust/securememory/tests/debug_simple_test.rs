use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use std::sync::Arc;
use std::thread;
use std::time::Duration;

#[test]
fn test_simple_concurrent_access() {
    eprintln!("Creating secret...");
    let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
    
    eprintln!("Creating reader thread...");
    let secret_clone = Arc::clone(&secret);
    let reader = thread::spawn(move || {
        eprintln!("Reader thread: starting");
        for i in 0..3 {
            eprintln!("Reader thread: access attempt {}", i);
            match secret_clone.with_bytes(|bytes| {
                eprintln!("Reader thread: got access to bytes: {:?}", bytes);
                thread::sleep(Duration::from_millis(10));
                Ok(())
            }) {
                Ok(_) => eprintln!("Reader thread: access {} successful", i),
                Err(e) => {
                    eprintln!("Reader thread: access {} failed: {:?}", i, e);
                    break;
                }
            }
        }
        eprintln!("Reader thread: done");
    });
    
    // Give reader a chance to start
    thread::sleep(Duration::from_millis(50));
    
    eprintln!("Main thread: attempting to close secret");
    match secret.close() {
        Ok(_) => eprintln!("Main thread: close successful"),
        Err(e) => eprintln!("Main thread: close failed: {:?}", e),
    }
    
    eprintln!("Main thread: waiting for reader thread");
    reader.join().unwrap();
    eprintln!("Test complete");
}

#[test]
fn test_close_while_accessing() {
    eprintln!("Creating secret...");
    let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
    
    let secret_clone = Arc::clone(&secret);
    let barrier = Arc::new(std::sync::Barrier::new(2));
    let barrier_clone = Arc::clone(&barrier);
    
    // Reader thread that holds access longer
    let reader = thread::spawn(move || {
        eprintln!("Reader: waiting at barrier");
        barrier_clone.wait();
        
        eprintln!("Reader: attempting access");
        match secret_clone.with_bytes(|bytes| {
            eprintln!("Reader: got access, sleeping...");
            thread::sleep(Duration::from_millis(100));
            eprintln!("Reader: done with access");
            Ok(())
        }) {
            Ok(_) => eprintln!("Reader: access successful"),
            Err(e) => eprintln!("Reader: access failed: {:?}", e),
        }
    });
    
    // Closer thread
    let secret_clone = Arc::clone(&secret);
    let barrier_clone = Arc::clone(&barrier);
    
    let closer = thread::spawn(move || {
        eprintln!("Closer: waiting at barrier");
        barrier_clone.wait();
        
        // Give reader time to start access
        thread::sleep(Duration::from_millis(10));
        
        eprintln!("Closer: attempting to close");
        match secret_clone.close() {
            Ok(_) => eprintln!("Closer: close successful"),
            Err(e) => eprintln!("Closer: close failed: {:?}", e),
        }
    });
    
    reader.join().unwrap();
    closer.join().unwrap();
    eprintln!("Test complete");
}
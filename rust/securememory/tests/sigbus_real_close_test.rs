use securememory::error::SecureMemoryError;
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Barrier};
use std::thread;

#[test]
fn test_real_close_race() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();

    // Arc is just for refcounting, not for mutations
    let arc_secret = Arc::new(secret);
    let closed = Arc::new(AtomicBool::new(false));

    // Just two threads to simplify
    let secret1 = Arc::clone(&arc_secret);
    let closed1 = Arc::clone(&closed);

    let reader = thread::spawn(move || loop {
        match secret1.with_bytes(|bytes| {
            assert_eq!(bytes, b"test");
            Ok(())
        }) {
            Ok(_) => {
                thread::yield_now();
            }
            Err(SecureMemoryError::SecretClosed) => {
                println!("Reader got SecretClosed");
                closed1.store(true, Ordering::Relaxed);
                break;
            }
            Err(e) => {
                panic!("Unexpected error: {:?}", e);
            }
        }
    });

    // Let reader run
    thread::sleep(std::time::Duration::from_millis(10));

    // Actually close the secret
    println!("Main thread closing secret");
    arc_secret.close().unwrap();

    reader.join().unwrap();

    assert!(closed.load(Ordering::Relaxed));
    println!("Real close race test passed");
}

#[test]
fn test_concurrent_close_attempts() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = Arc::new(factory.new(&mut data).unwrap());

    let barrier = Arc::new(Barrier::new(3));
    let mut handles = vec![];

    for i in 0..3 {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);

        let handle = thread::spawn(move || {
            barrier_clone.wait();
            match secret_clone.close() {
                Ok(_) => println!("Thread {} closed secret", i),
                Err(e) => println!("Thread {} close error: {}", i, e),
            }
        });

        handles.push(handle);
    }

    for handle in handles {
        handle.join().unwrap();
    }

    println!("Concurrent close attempts test passed");
}

#[test]
fn test_access_after_close() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();

    // Close the secret
    secret.close().unwrap();

    // Try to access after close
    match secret.with_bytes(|_| Ok(())) {
        Err(SecureMemoryError::SecretClosed) => {
            println!("Got expected SecretClosed error");
        }
        Ok(_) => panic!("Should not be able to access closed secret"),
        Err(e) => panic!("Unexpected error: {:?}", e),
    }

    println!("Access after close test passed");
}

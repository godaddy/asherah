use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};
use std::thread;
use std::time::Duration;

/// Simplest possible reproducer for SIGBUS
#[test]
fn test_simple_sigbus() {
    let factory = DefaultSecretFactory::new();

    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut orig).unwrap());

    // Thread 1: try to read
    let secret_clone = Arc::clone(&secret);
    let reader = thread::spawn(move || {
        println!("Reader: Starting read loop");
        for i in 0..1000 {
            match secret_clone.with_bytes(|bytes| {
                // Actually access the memory
                if bytes.len() > 0 {
                    let _ = bytes[0];
                }
                println!("Reader: Read {} successful", i);
                Ok(())
            }) {
                Ok(()) => {
                    // Add a small delay to create more race opportunities
                    std::thread::yield_now();
                }
                Err(e) => {
                    println!("Reader: Got expected error: {:?}", e);
                    return;
                }
            }
        }
        println!("Reader: Finished all reads");
    });

    // Give the reader a chance to start
    thread::sleep(Duration::from_millis(10));

    // Thread 2: close the secret
    println!("Main: Closing secret");
    secret.close().unwrap();
    println!("Main: Secret closed");

    // Wait for reader to finish
    reader.join().unwrap();
    println!("Main: Test completed");
}

/// Test with a tighter race condition
#[test]
fn test_tight_race_sigbus() {
    let factory = DefaultSecretFactory::new();

    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = Arc::new(factory.new(&mut orig).unwrap());
    let started = Arc::new(AtomicBool::new(false));

    // Thread 1: try to read
    let secret_clone = Arc::clone(&secret);
    let started_clone = Arc::clone(&started);
    let reader = thread::spawn(move || {
        started_clone.store(true, Ordering::Release);

        match secret_clone.with_bytes(|bytes| {
            // Sleep while holding the reference
            thread::sleep(Duration::from_millis(50));

            // Actually access the memory
            if bytes.len() > 0 {
                let _ = bytes[0];
            }
            Ok(())
        }) {
            Ok(()) => println!("Reader: Read successful"),
            Err(e) => println!("Reader: Got expected error: {:?}", e),
        }
    });

    // Wait for reader to start
    while !started.load(Ordering::Acquire) {
        thread::yield_now();
    }

    // Give reader time to acquire the lock
    thread::sleep(Duration::from_millis(10));

    // Thread 2: close the secret (this should wait)
    println!("Main: Closing secret");
    secret.close().unwrap();
    println!("Main: Secret closed");

    // Wait for reader to finish
    reader.join().unwrap();
    println!("Main: Test completed");
}

use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::{Duration, Instant};

#[test]
fn test_close_while_accessing() {
    let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
    let barrier = Arc::new(Barrier::new(2));

    // Start accessor thread
    let secret_clone = Arc::clone(&secret);
    let barrier_clone = Arc::clone(&barrier);

    let accessor = thread::spawn(move || {
        barrier_clone.wait();

        // First access
        secret_clone.with_bytes(|_| Ok(())).unwrap();

        // Sleep to let closer start
        thread::sleep(Duration::from_millis(10));

        // Try one more access - should fail
        match secret_clone.with_bytes(|_| Ok(())) {
            Ok(_) => println!("Second access succeeded (unexpected)"),
            Err(_) => println!("Second access failed (expected)"),
        }
    });

    barrier.wait();

    // Give accessor a chance to start
    thread::sleep(Duration::from_millis(5));

    println!("Attempting to close...");
    let start = Instant::now();
    secret.close().unwrap();
    let elapsed = start.elapsed();
    println!("Close completed in {:?}", elapsed);

    accessor.join().unwrap();
}

#[test]
fn test_access_without_close() {
    // Test that without closing, the access counter is properly maintained
    let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();

    // First access
    secret
        .with_bytes(|data| {
            assert_eq!(data, b"test data");
            Ok(())
        })
        .unwrap();

    // Second access
    secret
        .with_bytes(|data| {
            assert_eq!(data, b"test data");
            Ok(())
        })
        .unwrap();

    println!("Both accesses completed successfully");

    // Drop happens automatically
}

#[test]
fn test_direct_close_no_access() {
    // Test close without any prior access
    let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();

    println!("Closing without access...");
    let start = Instant::now();
    secret.close().unwrap();
    let elapsed = start.elapsed();
    println!("Close completed in {:?}", elapsed);
}

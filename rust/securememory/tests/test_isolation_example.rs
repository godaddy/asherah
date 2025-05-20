use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use securememory::test_utils::TestGuard;
use std::sync::Arc;
use std::thread;
use std::time::Duration;

/// This example demonstrates how to use the TestGuard to ensure test isolation
/// when working with protected memory tests.

// Using the TestGuard directly
#[test]
fn test_with_guard() {
    // Create a guard that will hold the lock for the duration of this test
    let _guard = TestGuard::new();

    // Now this test has exclusive access to protected memory
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"test secret data".to_vec()).unwrap();

    // Use the secret
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"test secret data");
            Ok(())
        })
        .unwrap();

    // Guard will be released when it goes out of scope
}

// Using the macro that wraps the TestGuard
use securememory::isolated_test;

isolated_test!(test_with_macro, {
    // Test code here automatically gets exclusive access
    let factory = DefaultSecretFactory::new();
    let secret = factory.new(&mut b"another test secret".to_vec()).unwrap();

    // Simulate some concurrent operations
    let secret = Arc::new(secret);
    let secret_clone = Arc::clone(&secret);

    let thread = thread::spawn(move || {
        for _ in 0..5 {
            secret_clone.with_bytes(|_| Ok(())).unwrap();
            thread::sleep(Duration::from_millis(1));
        }
    });

    // Main thread also uses the secret
    for _ in 0..5 {
        secret.with_bytes(|_| Ok(())).unwrap();
        thread::sleep(Duration::from_millis(1));
    }

    // Wait for thread to finish
    thread.join().unwrap();

    // Explicit close
    secret.close().unwrap();
});

#[test]
fn test_multiple_sequential() {
    // First operation with isolation
    {
        let _guard = TestGuard::new();
        let factory = DefaultSecretFactory::new();
        let secret = factory.new(&mut b"first operation".to_vec()).unwrap();
        secret.close().unwrap();
    } // Guard is dropped here, releasing the lock

    // Next operation with isolation
    {
        let _guard = TestGuard::new();
        let factory = DefaultSecretFactory::new();
        let secret = factory.new(&mut b"second operation".to_vec()).unwrap();
        secret.close().unwrap();
    } // Guard is dropped here, releasing the lock
}

// This test demonstrates what the TestGuard is protecting against
#[test]
#[ignore] // This test is intentionally ignored as it demonstrates a problem
fn test_demonstrate_problem() {
    let factory = DefaultSecretFactory::new();

    // Create many threads without isolation
    let mut handles = Vec::new();

    for i in 0..10 {
        let handle = thread::spawn(move || {
            // Each thread creates its own secret
            let sec_factory = DefaultSecretFactory::new();
            let data = format!("thread {} data", i);
            let secret = sec_factory.new(&mut data.into_bytes()).unwrap();

            // Access it a few times
            for _ in 0..5 {
                if let Err(e) = secret.with_bytes(|_| Ok(())) {
                    println!("Error accessing secret: {}", e);
                }
                thread::sleep(Duration::from_millis(1));
            }

            // Close it
            if let Err(e) = secret.close() {
                println!("Error closing secret: {}", e);
            }
        });

        handles.push(handle);
    }

    // Wait for all threads
    for handle in handles {
        handle.join().unwrap();
    }
}

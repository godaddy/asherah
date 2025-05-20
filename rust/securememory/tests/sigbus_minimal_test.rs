use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use securememory::test_utils::TestGuard;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;

#[test]
fn test_minimal_sigbus() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    // Test very minimal concurrent access
    let factory = DefaultSecretFactory::new();
    let secret = Arc::new(factory.new(&mut b"test".to_vec()).unwrap());

    // Just one reader, one closer
    let secret_reader = Arc::clone(&secret);
    let reader = thread::spawn(move || {
        for _ in 0..1000 {
            if secret_reader.with_bytes(|_| Ok(())).is_err() {
                break;
            }
        }
    });

    // Wait a tiny bit then close
    thread::sleep(std::time::Duration::from_millis(10));
    secret.close().unwrap();

    reader.join().unwrap();
    println!("Test passed");
}

#[test]
fn test_memory_lifecycle() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    let factory = DefaultSecretFactory::new();

    // Test 1: Create and immediately drop
    {
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        // Secret drops here
    }
    println!("Create/drop passed");

    // Test 2: Create, access, drop
    {
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        secret
            .with_bytes(|b| {
                assert_eq!(b, b"test");
                Ok(())
            })
            .unwrap();
        // Secret drops here
    }
    println!("Create/access/drop passed");

    // Test 3: Create, close, drop
    {
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        secret.close().unwrap();
        // Secret drops here
    }
    println!("Create/close/drop passed");

    // Test 4: Create, access, close, drop
    {
        let secret = factory.new(&mut b"test".to_vec()).unwrap();
        secret
            .with_bytes(|b| {
                assert_eq!(b, b"test");
                Ok(())
            })
            .unwrap();
        secret.close().unwrap();
        // Secret drops here
    }
    println!("Create/access/close/drop passed");
}

#[test]
fn test_concurrent_access_no_close() {
    // Ensure this test runs in isolation
    let _guard = TestGuard::new();
    let factory = DefaultSecretFactory::new();
    let secret = Arc::new(factory.new(&mut b"test".to_vec()).unwrap());
    let running = Arc::new(AtomicBool::new(true));

    // Spawn readers
    let mut handles = vec![];
    for i in 0..3 {
        let secret_clone = Arc::clone(&secret);
        let running_clone = Arc::clone(&running);
        let handle = thread::spawn(move || {
            while running_clone.load(Ordering::Acquire) {
                if let Ok(()) = secret_clone.with_bytes(|b| {
                    assert_eq!(b, b"test");
                    Ok(())
                }) {
                    thread::yield_now();
                }
            }
            println!("Reader {} done", i);
        });
        handles.push(handle);
    }

    // Let run for a bit
    thread::sleep(std::time::Duration::from_millis(100));

    // Stop readers
    running.store(false, Ordering::Release);

    // Wait for readers
    for handle in handles {
        handle.join().unwrap();
    }

    // Now it's safe to let secret drop
    println!("All readers done, dropping secret");
}

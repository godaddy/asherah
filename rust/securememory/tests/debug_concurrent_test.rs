use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretExtensions, SecretFactory};
use std::sync::Arc;
use std::thread;

#[test]
fn test_minimal_concurrent_access() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();
    let arc_secret = Arc::new(secret);

    // Just two threads, one operation each
    let secret1 = Arc::clone(&arc_secret);
    let secret2 = Arc::clone(&arc_secret);

    let handle1 = thread::spawn(move || {
        secret1
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            })
            .unwrap();
    });

    let handle2 = thread::spawn(move || {
        secret2
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            })
            .unwrap();
    });

    handle1.join().unwrap();
    handle2.join().unwrap();
}

#[test]
fn test_sequential_before_concurrent() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();

    // Access once to ensure it works
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"test");
            Ok(())
        })
        .unwrap();

    let arc_secret = Arc::new(secret);

    // Now try concurrent
    let secret1 = Arc::clone(&arc_secret);
    let secret2 = Arc::clone(&arc_secret);

    let handle1 = thread::spawn(move || {
        secret1
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            })
            .unwrap();
    });

    let handle2 = thread::spawn(move || {
        secret2
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            })
            .unwrap();
    });

    handle1.join().unwrap();
    handle2.join().unwrap();
}

#[test]
fn test_single_thread_multiple_access() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();

    // Multiple accesses in single thread
    for _ in 0..10 {
        secret
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"test");
                Ok(())
            })
            .unwrap();
    }
}

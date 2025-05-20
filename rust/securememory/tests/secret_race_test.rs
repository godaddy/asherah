use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc, Barrier,
};
use std::thread;
use std::time::Duration;

use securememory::error::SecureMemoryError;
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

const THREAD_COUNT: usize = 10;
const ITERATIONS: usize = 5;

/// This test attempts to trigger race conditions by having multiple threads
/// access the secret while another thread tries to close it.
#[test]
fn test_race_concurrent_close() {
    let factory = DefaultSecretFactory::new();

    for _ in 0..ITERATIONS {
        let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
        let copy_bytes = orig.clone();

        let secret = Arc::new(factory.new(&mut orig).unwrap());
        let closed = Arc::new(AtomicBool::new(false));

        // Create a barrier to synchronize all threads
        let barrier = Arc::new(Barrier::new(THREAD_COUNT + 1)); // +1 for the main thread

        let mut handles = Vec::with_capacity(THREAD_COUNT);

        // Create reader threads
        for _ in 0..THREAD_COUNT - 1 {
            let secret_clone = Arc::clone(&secret);
            let barrier_clone = Arc::clone(&barrier);
            let closed_clone = Arc::clone(&closed);
            let copy_bytes_clone = copy_bytes.clone();

            let handle = thread::spawn(move || {
                barrier_clone.wait(); // Wait for all threads to be ready

                // Keep trying to read the secret until the test signals it's closed
                while !closed_clone.load(Ordering::SeqCst) {
                    let result = secret_clone.with_bytes(|bytes| {
                        if bytes.len() == copy_bytes_clone.len() {
                            assert_eq!(bytes, copy_bytes_clone.as_slice());
                        }
                        Ok(())
                    });

                    // If the secret is closed, we expect an error
                    if let Err(e) = result {
                        match e {
                            SecureMemoryError::SecretClosed => {
                                // Mark as observed closed
                                closed_clone.store(true, Ordering::SeqCst);
                                break;
                            }
                            _ => panic!("Unexpected error: {:?}", e),
                        }
                    }

                    // Small yield to create more interleaving opportunities
                    thread::yield_now();
                }
            });

            handles.push(handle);
        }

        // Create a dedicated thread to close the secret
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        let closed_clone = Arc::clone(&closed);

        let closer_handle = thread::spawn(move || {
            barrier_clone.wait(); // Wait for all threads to be ready

            // Sleep briefly to give reader threads a chance to start
            thread::sleep(Duration::from_millis(5));

            // Actually close the secret instead of just setting a flag
            secret_clone.close().unwrap();

            // Signal that it's closed
            closed_clone.store(true, Ordering::SeqCst);
        });

        handles.push(closer_handle);

        // Release all threads
        barrier.wait();

        // Wait for all threads to complete
        for handle in handles {
            handle.join().unwrap();
        }
    }
}

/// This test simulates a race condition where a reader is trying to access
/// a secret that is being closed by another thread
#[test]
fn test_race_reader_with_concurrent_close() {
    let factory = DefaultSecretFactory::new();

    for _ in 0..ITERATIONS {
        let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
        let copy_bytes = orig.clone();

        let secret = Arc::new(factory.new(&mut orig).unwrap());
        let closed = Arc::new(AtomicBool::new(false));

        // Create a barrier to synchronize all threads
        let barrier = Arc::new(Barrier::new(THREAD_COUNT + 1)); // +1 for the main thread

        let mut handles = Vec::with_capacity(THREAD_COUNT);

        // Create reader threads using the reader() API
        for _ in 0..THREAD_COUNT - 1 {
            let secret_clone = Arc::clone(&secret);
            let barrier_clone = Arc::clone(&barrier);
            let closed_clone = Arc::clone(&closed);
            let copy_bytes_clone = copy_bytes.clone();

            let handle = thread::spawn(move || {
                barrier_clone.wait(); // Wait for all threads to be ready

                // Keep trying to read the secret until the test signals it's closed
                while !closed_clone.load(Ordering::SeqCst) {
                    let reader_result = secret_clone.reader();
                    let mut buffer = vec![0u8; copy_bytes_clone.len()];

                    use std::io::Read;
                    let result = match reader_result {
                        Ok(mut reader) => reader.read_exact(&mut buffer),
                        Err(e) => Err(std::io::Error::new(
                            std::io::ErrorKind::Other,
                            e.to_string(),
                        )),
                    };

                    // If the secret is closed, we expect an error
                    if let Err(_) = result {
                        // Any error is acceptable here since we're racing with close
                        closed_clone.store(true, Ordering::SeqCst);
                        break;
                    } else {
                        assert_eq!(buffer, copy_bytes_clone);
                    }

                    // Small yield to create more interleaving opportunities
                    thread::yield_now();
                }
            });

            handles.push(handle);
        }

        // Create a dedicated thread to close the secret
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);
        let closed_clone = Arc::clone(&closed);

        let closer_handle = thread::spawn(move || {
            barrier_clone.wait(); // Wait for all threads to be ready

            // Sleep briefly to give reader threads a chance to start
            thread::sleep(Duration::from_millis(5));

            // Actually close the secret instead of just setting a flag
            secret_clone.close().unwrap();

            // Signal that it's closed
            closed_clone.store(true, Ordering::SeqCst);
        });

        handles.push(closer_handle);

        // Release all threads
        barrier.wait();

        // Wait for all threads to complete
        for handle in handles {
            handle.join().unwrap();
        }
    }
}

/// Test for potential memory leaks by creating and destroying many secrets
#[test]
fn test_many_secrets_lifecycle() {
    let factory = DefaultSecretFactory::new();
    const NUM_SECRETS: usize = 1000;

    for _ in 0..3 {
        // Run this test multiple times
        let mut secrets = Vec::with_capacity(NUM_SECRETS);

        // Create many secrets
        for i in 0..NUM_SECRETS {
            let mut data = format!("secret-data-{}", i).into_bytes();
            let secret = factory.new(&mut data).unwrap();
            secrets.push(secret);
        }

        // Access each secret
        for secret in &secrets {
            secret.with_bytes(|_| Ok(())).unwrap();
        }

        // Close half the secrets explicitly
        for i in 0..(NUM_SECRETS / 2) {
            let mut secret = secrets.remove(0);
            secret.close().unwrap();
        }

        // The rest will be closed by the Drop implementation
    }
}

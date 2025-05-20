use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::Duration;

/// Test with high concurrency that previously caused SIGBUS
#[test]
fn test_high_concurrency_sigbus() {
    // Using more threads and iterations to stress the implementation
    let secret =
        Arc::new(ProtectedMemorySecretSimple::new(b"concurrent data with high stress").unwrap());
    const NUM_THREADS: usize = 10;
    const ITERATIONS: usize = 100;

    let barrier = Arc::new(Barrier::new(NUM_THREADS));
    let mut handles = Vec::new();

    // Create threads that rapidly access the secret
    for thread_id in 0..NUM_THREADS {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);

        let handle = thread::spawn(move || {
            println!("Thread {} waiting at barrier", thread_id);
            barrier_clone.wait();
            println!("Thread {} passed barrier", thread_id);

            for iter in 0..ITERATIONS {
                // Add small sleeps to encourage interleaving
                if iter % 10 == 0 {
                    thread::sleep(Duration::from_micros(1));
                }

                // Access secret
                match secret_clone.with_bytes(|bytes| {
                    if iter % 25 == 0 {
                        println!("Thread {} read {} bytes", thread_id, bytes.len());
                    }
                    Ok(())
                }) {
                    Ok(_) => {}
                    Err(e) => {
                        println!("Thread {} access failed: {}", thread_id, e);
                        break;
                    }
                }

                // Yield to increase interleaving chance
                thread::yield_now();
            }

            println!("Thread {} completed", thread_id);
        });

        handles.push(handle);
    }

    // Wait for all threads to complete
    for (i, handle) in handles.into_iter().enumerate() {
        println!("Waiting for thread {} to join", i);
        if let Err(e) = handle.join() {
            println!("Thread {} join failed: {:?}", i, e);
        } else {
            println!("Thread {} joined successfully", i);
        }
    }

    // Secret should still be usable
    println!("Final verification");
    assert!(!secret.is_closed());

    match secret.with_bytes(|bytes| {
        println!("Final access: verified {} bytes", bytes.len());
        Ok(())
    }) {
        Ok(_) => println!("Final access succeeded"),
        Err(e) => panic!("Final access failed unexpectedly: {}", e),
    }
}

/// Test with concurrent access and close which previously caused SIGBUS
#[test]
fn test_concurrent_access_and_close() {
    let secret =
        Arc::new(ProtectedMemorySecretSimple::new(b"concurrent access and close").unwrap());

    // Create multiple reader threads
    const NUM_READERS: usize = 5;
    let mut reader_handles = Vec::new();

    for reader_id in 0..NUM_READERS {
        let secret_clone = Arc::clone(&secret);

        let handle = thread::spawn(move || {
            for i in 0..50 {
                match secret_clone.with_bytes(|_| Ok(())) {
                    Ok(_) => {
                        if i % 20 == 0 {
                            println!("Reader {} access {} succeeded", reader_id, i);
                        }
                    }
                    Err(e) => {
                        println!("Reader {} access {} failed: {}", reader_id, i, e);
                        break; // Secret closed - exit
                    }
                }

                thread::yield_now();
            }
            println!("Reader {} completed", reader_id);
        });

        reader_handles.push(handle);
    }

    // Let readers start
    thread::sleep(Duration::from_millis(5));

    // Close the secret
    println!("Closing secret");
    secret.close().unwrap();
    println!("Secret closed");

    // Wait for all readers to finish
    for (i, handle) in reader_handles.into_iter().enumerate() {
        handle.join().unwrap();
        println!("Reader {} joined", i);
    }
}

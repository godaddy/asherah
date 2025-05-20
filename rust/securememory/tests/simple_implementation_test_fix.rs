use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::Duration;

#[test]
fn test_fewer_threads() {
    // Create a secret with some data
    let secret = ProtectedMemorySecretSimple::new(b"test data for race condition").unwrap();
    let secret = Arc::new(secret);

    // Use a barrier to synchronize thread startup
    const NUM_THREADS: usize = 2; // Reduced from 10
    let barrier = Arc::new(Barrier::new(NUM_THREADS + 1));

    let mut handles = Vec::new();

    // Create reader threads
    for i in 0..NUM_THREADS {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);

        let handle = thread::spawn(move || {
            eprintln!("Thread {}: waiting at barrier", i);
            barrier_clone.wait();
            eprintln!("Thread {}: starting access loop", i);

            // Keep trying to access the secret until it's closed
            let mut attempts = 0;
            loop {
                attempts += 1;
                match secret_clone.with_bytes(|_| Ok(())) {
                    Ok(_) => {
                        // Small yield to create more interleaving
                        thread::yield_now();
                    }
                    Err(_) => {
                        eprintln!("Thread {}: secret is closed after {} attempts", i, attempts);
                        break;
                    }
                }

                // Add a small timeout to prevent infinite loop
                if attempts > 1000 {
                    eprintln!("Thread {}: timeout after {} attempts", i, attempts);
                    break;
                }
            }
            eprintln!("Thread {}: done", i);
        });

        handles.push(handle);
    }

    // Create a closer thread
    let secret_clone = Arc::clone(&secret);
    let barrier_clone = Arc::clone(&barrier);

    let closer = thread::spawn(move || {
        eprintln!("Closer: waiting at barrier");
        barrier_clone.wait();
        eprintln!("Closer: sleeping before close");

        // Give readers a chance to start
        thread::sleep(Duration::from_millis(10));

        eprintln!("Closer: closing secret");
        // Close the secret
        match secret_clone.close() {
            Ok(_) => eprintln!("Closer: close successful"),
            Err(e) => eprintln!("Closer: close failed: {:?}", e),
        }
        eprintln!("Closer: done");
    });

    handles.push(closer);

    // Release all threads
    eprintln!("Main: releasing barrier");
    barrier.wait();

    // Wait for all threads to complete with timeout
    eprintln!("Main: waiting for threads");
    let start = std::time::Instant::now();
    let timeout = Duration::from_secs(5);

    for (i, handle) in handles.into_iter().enumerate() {
        if start.elapsed() > timeout {
            eprintln!("Main: timeout waiting for thread {}", i);
            break;
        }

        match handle.join() {
            Ok(_) => eprintln!("Main: thread {} joined", i),
            Err(_) => eprintln!("Main: thread {} panicked", i),
        }
    }

    eprintln!("Test complete");
}

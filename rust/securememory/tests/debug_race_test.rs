use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::{Secret, SecretExtensions};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::{Duration, Instant};

#[test]
fn test_debug_race_simple() {
    println!("Starting debug race test");
    let start = Instant::now();

    // Create a secret with some data
    let secret = ProtectedMemorySecretSimple::new(b"test data for race condition").unwrap();
    let secret = Arc::new(secret);

    // Use a barrier to synchronize thread startup
    const NUM_THREADS: usize = 2; // Start small
    let barrier = Arc::new(Barrier::new(NUM_THREADS + 1));

    let mut handles = Vec::new();

    // Create reader threads
    for i in 0..NUM_THREADS {
        let secret_clone = Arc::clone(&secret);
        let barrier_clone = Arc::clone(&barrier);

        let handle = thread::spawn(move || {
            println!("Thread {} waiting at barrier", i);
            barrier_clone.wait();
            println!("Thread {} starting", i);

            let mut access_count = 0;

            // Keep trying to access the secret until it's closed
            loop {
                match secret_clone.with_bytes(|_| Ok(())) {
                    Ok(_) => {
                        access_count += 1;
                        if access_count % 1000 == 0 {
                            println!("Thread {} made {} accesses", i, access_count);
                        }
                        thread::yield_now();
                    }
                    Err(_) => {
                        println!(
                            "Thread {} saw closed secret after {} accesses",
                            i, access_count
                        );
                        break;
                    }
                }
            }

            println!("Thread {} exiting", i);
        });

        handles.push(handle);
    }

    // Create a closer thread
    let secret_clone = Arc::clone(&secret);
    let barrier_clone = Arc::clone(&barrier);

    let closer = thread::spawn(move || {
        println!("Closer waiting at barrier");
        barrier_clone.wait();
        println!("Closer starting");

        // Give readers a chance to start
        thread::sleep(Duration::from_millis(5));

        println!("Closer attempting to close...");
        secret_clone.close().unwrap();
        println!("Closer finished closing");
    });

    // Release all threads
    println!("Main thread releasing barrier");
    barrier.wait();

    // Add timeout for join
    let join_timeout = Duration::from_secs(5);

    for (i, handle) in handles.into_iter().enumerate() {
        println!("Waiting for thread {} to complete", i);
        let join_start = Instant::now();

        match handle.join() {
            Ok(_) => println!("Thread {} joined successfully", i),
            Err(e) => {
                println!("Thread {} failed to join: {:?}", i, e);
                if join_start.elapsed() > join_timeout {
                    println!("Thread {} join timed out", i);
                }
            }
        }
    }

    println!("Waiting for closer to complete");
    match closer.join() {
        Ok(_) => println!("Closer joined successfully"),
        Err(e) => println!("Closer failed to join: {:?}", e),
    }

    let elapsed = start.elapsed();
    println!("Test completed in {:?}", elapsed);

    if elapsed > Duration::from_secs(10) {
        panic!("Test took too long: {:?}", elapsed);
    }
}

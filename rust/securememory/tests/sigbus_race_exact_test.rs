use securememory::protected_memory::factory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::sync::{Arc, Barrier};
use std::thread;
use std::time::Duration;

#[test]
fn test_exact_race_pattern() {
    let factory = DefaultSecretFactory::new();

    // Create a secret with some data
    let mut data = b"test data for race condition".to_vec();
    let secret = factory.new(&mut data).unwrap();
    let secret = Arc::new(secret);

    // Use a barrier to synchronize thread startup
    let barrier = Arc::new(Barrier::new(2));

    // Clone for the reader thread
    let secret_reader = Arc::clone(&secret);
    let barrier_reader = Arc::clone(&barrier);

    // Thread 1: Reader that accesses repeatedly
    let reader = thread::spawn(move || {
        barrier_reader.wait();

        // Keep trying to access until we get an error
        for _ in 0..100 {
            match secret_reader.with_bytes(|_| Ok(())) {
                Ok(_) => {
                    // Small yield to create interleaving
                    thread::yield_now();
                }
                Err(_) => {
                    // Expected error when closed
                    break;
                }
            }
        }
    });

    // Thread 2: Closer that closes the secret
    let secret_closer = Arc::clone(&secret);
    let barrier_closer = Arc::clone(&barrier);

    let closer = thread::spawn(move || {
        barrier_closer.wait();

        // Give reader a chance to start
        thread::sleep(Duration::from_micros(100));

        // Close the secret
        secret_closer.close().unwrap();
    });

    // Wait for both threads to complete
    reader.join().unwrap();
    closer.join().unwrap();
}

#[test]
fn test_simple_close_while_reading() {
    let factory = DefaultSecretFactory::new();

    // Create a secret with some data
    let mut data = b"test data".to_vec();
    let secret = factory.new(&mut data).unwrap();
    let secret = Arc::new(secret);

    let secret_reader = Arc::clone(&secret);
    let reader = thread::spawn(move || {
        // Start reading in a loop
        loop {
            match secret_reader.with_bytes(|bytes| {
                println!("Reader: Accessing bytes of length {}", bytes.len());
                // Simulate some work
                thread::sleep(Duration::from_micros(10));
                Ok(())
            }) {
                Ok(_) => {
                    println!("Reader: Access successful");
                    continue;
                }
                Err(e) => {
                    println!("Reader: Got error: {:?}", e);
                    break;
                }
            }
        }
        println!("Reader: Exiting");
    });

    // Give the reader time to start
    thread::sleep(Duration::from_millis(1));

    // Close while reader might be active
    println!("Main: Closing secret");
    secret.close().unwrap();
    println!("Main: Secret closed");

    // Wait for reader to finish
    reader.join().unwrap();
    println!("Main: Test complete");
}

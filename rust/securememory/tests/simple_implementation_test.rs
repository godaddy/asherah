mod tests {
    use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
    use securememory::secret::{Secret, SecretExtensions};
    use std::sync::{Arc, Mutex};
    use std::thread;
    use std::time::Duration;

    #[test]
    fn test_race_concurrent_close_simple() {
        // Create a secret with some data
        let secret = Arc::new(ProtectedMemorySecretSimple::new(b"test data").unwrap());
        
        // Create a flag to indicate if any thread had an error
        let error_flag = Arc::new(Mutex::new(false));
        
        // Create a reader thread that accesses the secret
        let secret_clone = Arc::clone(&secret);
        let error_flag_clone = Arc::clone(&error_flag);
        
        let reader = thread::spawn(move || {
            // Access the secret a few times
            for _ in 0..5 {
                match secret_clone.with_bytes(|bytes| {
                    assert_eq!(bytes, b"test data");
                    Ok(())
                }) {
                    Ok(_) => thread::sleep(Duration::from_millis(1)),
                    Err(e) => {
                        println!("Reader error: {:?}", e);
                        // Secret might have been closed, which is expected
                        break;
                    }
                }
            }
            
            // Try to access after it should be closed
            thread::sleep(Duration::from_millis(10));
            
            match secret_clone.with_bytes(|_| Ok(())) {
                Ok(_) => {
                    println!("ERROR: Able to access secret after it should be closed");
                    *error_flag_clone.lock().unwrap() = true;
                },
                Err(_) => {
                    // This is the expected behavior - secret is closed
                }
            }
        });
        
        // Create a closer thread
        let secret_clone = Arc::clone(&secret);
        let closer = thread::spawn(move || {
            // Give the reader a chance to start
            thread::sleep(Duration::from_millis(5));
            
            // Close the secret
            println!("Closer: Closing the secret");
            let start = std::time::Instant::now();
            let result = secret_clone.close();
            let elapsed = start.elapsed();
            println!("Closer: Close completed in {:?}", elapsed);
            
            // The close should succeed
            assert!(result.is_ok(), "Close failed: {:?}", result);
        });
        
        // Wait for threads to complete
        reader.join().expect("Reader thread panicked");
        closer.join().expect("Closer thread panicked");
        
        // Check if any thread had an error
        assert!(!*error_flag.lock().unwrap(), "Test failed due to unexpected error");
    }
}
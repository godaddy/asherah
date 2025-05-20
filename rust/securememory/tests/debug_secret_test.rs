use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretExtensions, SecretFactory};
use std::sync::Arc;
use std::thread;

#[test]
fn test_simple_creation_use_drop() {
    println!("Test: simple creation, use, drop");

    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();

    let secret = factory.new(&mut data).unwrap();
    println!("Created secret");

    secret
        .with_bytes(|bytes| {
            println!("Accessing bytes: {:?}", bytes);
            assert_eq!(bytes, b"test");
            Ok(())
        })
        .unwrap();

    println!("About to drop secret");
    drop(secret);
    println!("Secret dropped successfully");
}

#[test]
fn test_arc_single_thread() {
    println!("Test: Arc single thread");

    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();

    let secret = Arc::new(factory.new(&mut data).unwrap());
    println!("Created Arc<secret>");

    let clone1 = Arc::clone(&secret);
    let clone2 = Arc::clone(&secret);

    println!("Strong count: {}", Arc::strong_count(&secret));

    // Use clone1
    clone1
        .with_bytes(|bytes| {
            println!("Clone1 accessing bytes: {:?}", bytes);
            Ok(())
        })
        .unwrap();

    drop(clone1);
    println!(
        "Dropped clone1, strong count: {}",
        Arc::strong_count(&secret)
    );

    drop(clone2);
    println!(
        "Dropped clone2, strong count: {}",
        Arc::strong_count(&secret)
    );

    drop(secret);
    println!("Dropped original, test complete");
}

#[test]
fn test_arc_drop_race() {
    println!("Test: Arc drop with threads");

    for i in 0..5 {
        println!("Iteration {}", i);

        let factory = DefaultSecretFactory::new();
        let mut data = b"test".to_vec();

        let secret = Arc::new(factory.new(&mut data).unwrap());

        let mut handles = vec![];

        // Create 3 threads that all have clones
        for j in 0..3 {
            let secret_clone = Arc::clone(&secret);

            let handle = thread::spawn(move || {
                println!("Thread {} starting", j);

                // Access the data
                secret_clone
                    .with_bytes(|bytes| {
                        println!("Thread {} accessing data", j);
                        Ok(())
                    })
                    .unwrap();

                // Sleep a bit
                thread::sleep(std::time::Duration::from_millis(10));

                println!("Thread {} dropping clone", j);
                drop(secret_clone);
                println!("Thread {} done", j);
            });

            handles.push(handle);
        }

        // Drop the original
        println!("Main thread dropping original");
        drop(secret);

        // Wait for all threads
        for handle in handles {
            handle.join().unwrap();
        }

        println!("Iteration {} complete\n", i);
    }

    println!("All iterations complete");
}

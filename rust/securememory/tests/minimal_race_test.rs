use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretExtensions, SecretFactory};
use std::sync::Arc;
use std::thread;

#[test]
fn test_minimal_race() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test".to_vec();
    let secret = factory.new(&mut data).unwrap();
    let arc_secret = Arc::new(secret);

    let threads = (0..2)
        .map(|i| {
            let secret_clone = Arc::clone(&arc_secret);
            thread::spawn(move || {
                println!("Thread {} starting", i);
                for j in 0..5 {
                    println!("Thread {} iteration {}", i, j);
                    match secret_clone.with_bytes(|bytes| {
                        assert_eq!(bytes, b"test");
                        Ok(())
                    }) {
                        Ok(_) => println!("Thread {} iteration {} success", i, j),
                        Err(e) => println!("Thread {} iteration {} error: {}", i, j, e),
                    }
                    thread::yield_now();
                }
                println!("Thread {} done", i);
            })
        })
        .collect::<Vec<_>>();

    for t in threads {
        t.join().unwrap();
    }
}

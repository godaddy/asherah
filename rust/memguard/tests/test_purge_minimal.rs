use memguard::{purge, Buffer};

#[test]
fn test_minimal_purge_deadlock() {
    println!("Creating buffer");
    let buffer = Buffer::new(32).unwrap();

    println!("Calling purge");
    purge();

    println!("Checking if buffer is destroyed");
    assert!(buffer.destroyed());
    println!("Test complete");
}

use crate::Buffer;
use std::thread;
use std::time::Duration;

#[test]
fn test_thread_spawning_during_exit() {
    // Create significant state that needs cleanup
    let _b1 = Buffer::new(32).unwrap();
    let _b2 = Buffer::new(64).unwrap();
    let _b3 = Buffer::new(128).unwrap();
    
    // This simulates the problem where threads might be spawned during cleanup
    thread::spawn(|| {
        thread::sleep(Duration::from_millis(10));
    });
    
    // Make sure we have active threads
    thread::sleep(Duration::from_millis(1));
}
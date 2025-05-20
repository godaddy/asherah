#[cfg(test)]
mod test_minimal2 {
    #[test]
    fn test_minimal2_creation() {
        use std::time::{Instant, Duration};
        use std::sync::atomic::{AtomicBool, Ordering};
        use std::thread;
        
        
        use std::sync::Arc;
        
        let started = Arc::new(AtomicBool::new(false));
        let failed = Arc::new(AtomicBool::new(false));
        
        let started_clone = Arc::clone(&started);
        let failed_clone = Arc::clone(&failed);
        
        // Spawn a thread to actually run the test
        let test_thread = thread::spawn(move || {
            started_clone.store(true, Ordering::SeqCst);
            
            match super::super::Buffer::new(32) {
                Ok(buffer) => {
                    buffer.destroy().unwrap();
                },
                Err(e) => {
                    failed_clone.store(true, Ordering::SeqCst);
                }
            }
        });
        
        // Wait up to 5 seconds for the test to complete
        let timeout = Duration::from_secs(5);
        let start = Instant::now();
        
        while !test_thread.is_finished() && start.elapsed() < timeout {
            thread::sleep(Duration::from_millis(100));
        }
        
        if !test_thread.is_finished() {
            panic!("Test hung during Buffer::new");
        } else {
            test_thread.join().unwrap();
            
            if failed.load(Ordering::SeqCst) {
                panic!("Buffer creation failed");
            }
        }
        
    }
}
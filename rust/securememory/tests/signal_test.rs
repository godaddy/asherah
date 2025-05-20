use securememory::signal::{self, Signal};

#[test]
fn test_signal_handler_registration() {
    // Test that we can register a signal handler without errors
    let result = signal::catch_signal(|_| {});
    assert!(result.is_ok());
}

#[test]
fn test_catch_interrupt() {
    // Test the catch_interrupt convenience function
    let result = signal::catch_interrupt();
    assert!(result.is_ok());
}

#[test]
fn test_register_multiple_handlers() {
    // Test that we can register multiple handlers sequentially
    let result1 = signal::catch_signal(|_| println!("Handler 1"));
    let result2 = signal::catch_signal(|_| println!("Handler 2"));
    
    assert!(result1.is_ok());
    assert!(result2.is_ok());
}

#[test]
fn test_signal_conversion() {
    // Test signal conversion to/from i32
    let signals = [
        Signal::Interrupt,
        Signal::Terminate,
        Signal::Other(99),
    ];
    
    for &sig in &signals {
        let val: i32 = sig.into();
        let converted: Signal = val.into();
        
        // For regular signals, we should get the same enum variant back
        if let Signal::Other(n) = sig {
            assert_eq!(converted, Signal::Other(n));
        } else {
            assert_eq!(converted, sig);
        }
    }
}

// This test is commented out because it would exit the test process
// which is not what we want in regular test runs
/*
#[test]
fn test_exit_function() {
    // Create a flag to track if our handler was called
    let flag = Arc::new(AtomicBool::new(false));
    let flag_clone = flag.clone();
    
    // Register a signal handler that sets the flag
    let _ = signal::catch_signal(move |_| {
        flag_clone.store(true, Ordering::SeqCst);
    });
    
    // Call exit, which should call our handler
    // Note: This would normally terminate the process, so in a real test
    // we would need to fork or use a different approach
    signal::exit(0);
    
    // Assert that our handler was called
    assert!(flag.load(Ordering::SeqCst));
}
*/
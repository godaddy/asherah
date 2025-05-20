use std::sync::Once;

static INIT: Once = Once::new();

pub fn setup_abort_handler() {
    INIT.call_once(|| {
        unsafe {
            use libc::{signal, SIGABRT, SIGPIPE, SIG_IGN};
            
            // Ignore SIGPIPE to prevent broken pipe issues
            signal(SIGPIPE, SIG_IGN);
            
            extern "C" fn abort_handler(sig: i32) {
                eprintln!("\n=== SIGABRT INTERCEPTED ===");
                eprintln!("Signal: {}", sig);
                eprintln!("Thread: {:?}", std::thread::current().name());
                eprintln!("Thread ID: {:?}", std::thread::current().id());
                
                // Capture backtrace
                let bt = std::backtrace::Backtrace::capture();
                eprintln!("Backtrace:\n{}", bt);
                eprintln!("========================\n");
                
                // Don't re-raise, just exit
                std::process::exit(134); // 128 + SIGABRT
            }
            
            signal(SIGABRT, abort_handler as *const () as libc::sighandler_t);
        }
    });
}

#[ctor::ctor]
fn init() {
    setup_abort_handler();
}
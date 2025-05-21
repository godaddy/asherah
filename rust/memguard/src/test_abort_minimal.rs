use crate::Buffer;

#[test]
fn test_minimal_abort_trigger() {
    eprintln!("Starting minimal abort test");

    // Install a simple abort handler
    unsafe {
        use libc::{signal, SIGABRT, SIG_DFL};

        extern "C" fn handler(_: i32) {
            eprintln!("\n*** SIGABRT CAUGHT ***");
            let bt = std::backtrace::Backtrace::capture();
            eprintln!("Backtrace:\n{}", bt);
            eprintln!("*******************\n");

            // Reset handler and abort
            unsafe {
                signal(SIGABRT, SIG_DFL);
            }
            std::process::abort();
        }

        signal(SIGABRT, handler as *const () as libc::sighandler_t);
    }

    // Create and destroy a buffer
    let _b = Buffer::new(32).unwrap();
    eprintln!("Test completed successfully");
}

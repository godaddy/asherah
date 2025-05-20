use memguard::{catch_interrupt, catch_signal, safe_exit};
use signal_hook::consts::signal::SIGINT;
use std::net::TcpListener;
use std::sync::Arc;
use std::sync::Mutex;
use std::thread;
use std::time::Duration;

fn main() {
    if let Some(mode) = std::env::args().nth(1) {
        match mode.as_str() {
            "interrupt" => {
                eprintln!("Subprocess: Starting interrupt test");
                catch_interrupt().expect("Subprocess: catch_interrupt failed");
                eprintln!("Subprocess: catch_interrupt registered");

                // Give time for signal handler to be fully registered
                thread::sleep(Duration::from_millis(100));

                // Send SIGINT to self
                eprintln!(
                    "Subprocess: Sending SIGINT to self (PID: {})",
                    std::process::id()
                );
                signal_hook::low_level::raise(SIGINT).expect("Failed to raise SIGINT");
                eprintln!("Subprocess: SIGINT sent, waiting for handler");
                thread::sleep(Duration::from_secs(5));
                eprintln!("Subprocess: Timed out waiting for signal handler to exit (interrupt).");
                safe_exit(3);
            }
            "signal" => {
                eprintln!("Subprocess: Starting signal test");
                let listener = TcpListener::bind("127.0.0.1:0")
                    .expect("Subprocess: Failed to bind TCP listener");
                let _addr = listener
                    .local_addr()
                    .expect("Subprocess: Failed to get local addr");

                // Store listener in a global static for the handler to access and close.
                static LISTENER_HOLDER: Mutex<Option<TcpListener>> = Mutex::new(None);
                *LISTENER_HOLDER.lock().unwrap() = Some(listener);

                let handler = Arc::new(|signal_code: i32| {
                    eprintln!(
                        "Subprocess: Custom signal handler called for signal {}.",
                        signal_code
                    );
                    if let Some(listener_to_drop) = LISTENER_HOLDER.lock().unwrap().take() {
                        drop(listener_to_drop); // This closes the listener
                        eprintln!("Subprocess: TCP listener closed by signal handler.");
                    } else {
                        eprintln!("Subprocess: Signal handler: No listener found to close.");
                    }
                });

                catch_signal(handler, &[SIGINT]).expect("Subprocess: catch_signal failed");

                // Send SIGINT to self
                signal_hook::low_level::raise(SIGINT).expect("Failed to raise SIGINT");
                eprintln!("Subprocess: SIGINT sent, waiting for handler");
                thread::sleep(Duration::from_secs(5));
                eprintln!("Subprocess: Timed out waiting for signal handler to exit.");
                safe_exit(3);
            }
            _ => {
                eprintln!("Unknown mode: {}", mode);
                std::process::exit(4);
            }
        }
    } else {
        eprintln!("No mode specified");
        std::process::exit(4);
    }
}

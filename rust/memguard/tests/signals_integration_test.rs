#[cfg(unix)] // These tests rely on POSIX signals and process handling.
mod signals_tests {
    use std::process::{Command, Stdio};
    
    #[test]
    fn test_catch_signal_integration() {
        // Build the example program
        let build_output = Command::new("cargo")
            .args(&["build", "--example", "signal_test"])
            .output()
            .expect("Failed to build signal_test example");
            
        if !build_output.status.success() {
            eprintln!("Build stderr: {}", String::from_utf8_lossy(&build_output.stderr));
            panic!("Failed to build signal_test example");
        }
        
        // Run the test with signal mode
        let mut cmd = Command::new("cargo");
        cmd.args(&["run", "--example", "signal_test", "--", "signal"]);
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::piped());

        let output = cmd.output().expect("Failed to execute subprocess for TestCatchSignal");
        let status = output.status;
        let stdout_str = String::from_utf8_lossy(&output.stdout);
        let stderr_str = String::from_utf8_lossy(&output.stderr);

        eprintln!("Subprocess exited with code: {:?}", status.code());
        eprintln!("Subprocess stdout: {}", stdout_str);
        eprintln!("Subprocess stderr: {}", stderr_str);

        // The subprocess should call safe_exit(1) via the signal handling mechanism.
        assert_eq!(status.code(), Some(1), 
            "Subprocess for TestCatchSignal exited with unexpected code. Status: {:?}\nStdout: {}\nStderr: {}", 
            status,
            stdout_str,
            stderr_str
        );

        // Verify the custom handler was called and the listener was closed
        assert!(stderr_str.contains("Subprocess: Custom signal handler called for signal"), "Custom signal handler message not found in stderr.");
        assert!(stderr_str.contains("Subprocess: TCP listener closed by signal handler."), "Listener close message not found in stderr.");
    }

    #[test]
    fn test_catch_interrupt_integration() {
        // Build the example program
        let build_output = Command::new("cargo")
            .args(&["build", "--example", "signal_test"])
            .output()
            .expect("Failed to build signal_test example");
            
        if !build_output.status.success() {
            eprintln!("Build stderr: {}", String::from_utf8_lossy(&build_output.stderr));
            panic!("Failed to build signal_test example");
        }
        
        // Run the test with interrupt mode
        let mut cmd = Command::new("cargo");
        cmd.args(&["run", "--example", "signal_test", "--", "interrupt"]);
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::piped());

        let output = cmd.output().expect("Failed to execute subprocess for TestCatchInterrupt");
        let status = output.status;
        let stdout_str = String::from_utf8_lossy(&output.stdout);
        let stderr_str = String::from_utf8_lossy(&output.stderr);
        
        eprintln!("Subprocess exited with code: {:?}", status.code());
        eprintln!("Subprocess stdout: {}", stdout_str);
        eprintln!("Subprocess stderr: {}", stderr_str);
        
        // Exit code 0 means the subprocess ran but signal handler wasn't called
        // Exit code 1 means safe_exit was called (what we expect)
        // Exit code 3 means timeout
        assert!(
            status.code() == Some(1), 
            "Subprocess for TestCatchInterrupt exited with unexpected code. Expected 1, got {:?}. Status: {:?}\nStdout: {}\nStderr: {}", 
            status.code(),
            status,
            stdout_str,
            stderr_str
        );

        // Verify the default interrupt handler logged to stderr (due to our change in signals.rs)
        assert!(stderr_str.contains("memguard: Interrupt signal"), "Default interrupt handler log not found in stderr. Got: {}", stderr_str);
        assert!(stderr_str.contains("caught. Cleaning up..."), "Cleanup message not found in stderr. Got: {}", stderr_str);
    }
}
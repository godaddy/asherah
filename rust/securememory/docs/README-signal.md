# Signal Handling in SecureMemory

This document describes the signal handling system in the `securememory` crate, which ensures that sensitive memory is properly cleaned up when a program terminates unexpectedly due to signals.

## Overview

The signal handling module is designed to:

1. Catch common termination signals like SIGINT, SIGTERM, SIGSEGV, etc.
2. Execute a user-defined handler when a signal is received
3. Safely clean up all secure memory before the program terminates
4. Exit the program with a proper exit code

This ensures that no sensitive data remains in memory after the program terminates, even if killed abruptly.

## Usage

### Basic Usage

For simple applications, you can just register a handler for interrupt signals (Ctrl+C):

```rust
use securememory::signal;

fn main() {
    // Register a handler for SIGINT
    signal::catch_interrupt().expect("Failed to set up signal handler");
    
    // Continue with your program...
}
```

### Custom Signal Handling

For more advanced cases, you can register a custom handler that will be called when a signal is received:

```rust
use securememory::signal;

fn main() {
    // Register a custom handler
    signal::catch_signal(|sig| {
        println!("Received signal: {}", sig);
        // Perform any custom cleanup...
    }).expect("Failed to set up signal handler");
    
    // Continue with your program...
}
```

### Handling Multiple Signals

You can also register different handlers for different signals:

```rust
use securememory::signal::{self, Signal};

fn main() {
    // Register a handler for interrupt and termination signals
    signal::catch_signals(|sig| {
        match sig {
            Signal::Interrupt => println!("Interrupted!"),
            Signal::Terminate => println!("Terminated!"),
            _ => println!("Other signal: {}", sig),
        }
    }, &[Signal::Interrupt, Signal::Terminate])
    .expect("Failed to set up signal handlers");
    
    // Continue with your program...
}
```

### Manual Termination

If you need to terminate your program manually while ensuring secure cleanup, you can use the `exit` function:

```rust
use securememory::signal;

fn some_function() {
    if some_critical_error_occurred {
        // Clean up secure memory and exit
        signal::exit(1);
    }
}
```

## Supported Signals

The following signals are supported:

- SIGINT (Interrupt, typically from Ctrl+C)
- SIGTERM (Termination signal)
- SIGSEGV (Segmentation fault)
- SIGILL (Illegal instruction)
- SIGBUS (Bus error)
- SIGABRT (Abort)
- SIGFPE (Floating-point exception)

## Design Details

### Architecture

The signal handling system uses a combination of the `ctrlc` and `signal-hook` crates to safely handle signals across different platforms. It follows these principles:

1. **Thread Safety**: All operations are thread-safe and work correctly in multi-threaded applications
2. **Single Handler Thread**: A dedicated thread is used to process signals, avoiding issues with signal handlers in multi-threaded environments
3. **Message Passing**: Signals are sent to the handler thread via a channel to ensure safe processing
4. **Cleanup Before Exit**: Memory is always cleaned up before the program exits

### Signal Flow

When a signal is received:

1. The OS signal handler sends a message to the handler thread
2. The handler thread executes the user-defined handler
3. The `purge` function is called to clean up all secure memory
4. The program exits with code 1

### Limitations

- Signal handlers should be kept simple and avoid allocations or other complex operations
- Due to OS limitations, not all signals can be caught on all platforms
- In rare cases, the program might be terminated before cleanup is complete (e.g., SIGKILL)

## Integration with Other Modules

The signal handling system integrates with the rest of the `securememory` crate to ensure that all secure memory is properly cleaned up. This includes:

- Memory allocated by `ProtectedMemorySecret`
- Buffers managed by the `memguard` module
- Enclaves that contain encrypted data
- Stream buffers used for processing large amounts of data

## Platform Support

The signal handling system works on:

- Linux
- macOS
- Windows (with some limitations due to different signal handling)

## Future Improvements

Planned enhancements for the signal handling system:

1. Global registry of active secrets for more comprehensive cleanup
2. More fine-grained control over signal handling
3. Better integration with panic handling
4. Support for custom exit codes for different signals
use std::fs::File;
use std::io::{self, BufWriter, Write};
use std::path::Path;

#[cfg(target_os = "linux")]
use std::fs::read_to_string;

use crate::{Options, Stats};

/// MemoryTracker tracks memory usage of the current process
pub struct MemoryTracker {
    writer: Option<BufWriter<File>>,
    header_printed: bool,
}

impl MemoryTracker {
    /// Create a new memory tracker
    pub fn new() -> Self {
        Self {
            writer: None,
            header_printed: false,
        }
    }

    /// Create a new memory tracker with file output
    pub fn with_output(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::create(path)?;
        let writer = BufWriter::new(file);

        Ok(Self {
            writer: Some(writer),
            header_printed: false,
        })
    }

    /// Sample current memory usage and update stats
    pub fn sample(&mut self, stats: &mut Stats) {
        // Get current memory usage
        let (heap_mb, stack_mb, total_mb) = self.get_current_memory_usage();

        // Update stats
        stats.memory_heap_mb = heap_mb;
        stats.memory_stack_mb = stack_mb;
        stats.memory_total_mb = total_mb;

        // Write to file if configured
        if let Some(writer) = &mut self.writer {
            if !self.header_printed {
                writeln!(writer, "Requests,HeapUsage,StackUsage,TotalUsage")
                    .expect("Failed to write header");

                self.header_printed = true;
            }

            writeln!(
                writer,
                "{},{:.2},{:.2},{:.2}",
                stats.request_count,
                stats.memory_heap_mb,
                stats.memory_stack_mb,
                stats.memory_total_mb
            )
            .expect("Failed to write memory stats");
        }
    }

    // Get current memory usage (platform-specific implementations)
    fn get_current_memory_usage(&self) -> (f64, f64, f64) {
        #[cfg(target_os = "linux")]
        {
            self.get_linux_memory_usage()
        }

        #[cfg(target_os = "macos")]
        {
            self.get_macos_memory_usage()
        }

        #[cfg(target_os = "windows")]
        {
            self.get_windows_memory_usage()
        }

        #[cfg(not(any(target_os = "linux", target_os = "macos", target_os = "windows")))]
        {
            // Default implementation for unsupported platforms
            (0.0, 0.0, 0.0)
        }
    }

    #[cfg(target_os = "linux")]
    fn get_linux_memory_usage(&self) -> (f64, f64, f64) {
        // Read process memory information from /proc/self/status
        if let Ok(status) = read_to_string("/proc/self/status") {
            let mut vm_rss = 0.0;
            let mut vm_size = 0.0;
            let mut vm_stack = 0.0;

            for line in status.lines() {
                if line.starts_with("VmRSS:") {
                    vm_rss = parse_proc_memory_value(line);
                } else if line.starts_with("VmSize:") {
                    vm_size = parse_proc_memory_value(line);
                } else if line.starts_with("VmStk:") {
                    vm_stack = parse_proc_memory_value(line);
                }
            }

            // Convert to MB (assuming parsed values are in KB)
            let heap_mb = (vm_rss - vm_stack) / 1024.0;
            let stack_mb = vm_stack / 1024.0;
            let total_mb = vm_size / 1024.0;

            return (heap_mb, stack_mb, total_mb);
        }

        // Default if reading fails
        (0.0, 0.0, 0.0)
    }

    #[cfg(target_os = "macos")]
    fn get_macos_memory_usage(&self) -> (f64, f64, f64) {
        // macOS implementation using process_memory crate or sysctl
        // For this demo, we'll use approximate values
        let total_mb = 100.0; // Example value
        let heap_mb = 80.0; // Example value
        let stack_mb = 20.0; // Example value

        (heap_mb, stack_mb, total_mb)
    }

    #[cfg(target_os = "windows")]
    fn get_windows_memory_usage(&self) -> (f64, f64, f64) {
        // Windows implementation using GetProcessMemoryInfo
        // For this demo, we'll use approximate values
        let total_mb = 100.0; // Example value
        let heap_mb = 80.0; // Example value
        let stack_mb = 20.0; // Example value

        (heap_mb, stack_mb, total_mb)
    }
}

#[cfg(target_os = "linux")]
fn parse_proc_memory_value(line: &str) -> f64 {
    let parts: Vec<&str> = line.split_whitespace().collect();
    if parts.len() >= 2 {
        if let Ok(value) = parts[1].parse::<f64>() {
            return value;
        }
    }
    0.0
}

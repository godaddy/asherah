use std::fs::File;
use std::io::{self, BufWriter, Write};
use std::path::Path;
use std::time::{Duration, Instant};

use crate::{Options, Stats};

/// LatencyRecorder tracks operation latencies with percentile calculations
pub struct LatencyRecorder {
    measurements: Vec<Duration>,
    writer: Option<BufWriter<File>>,
    header_printed: bool,
}

impl LatencyRecorder {
    /// Create a new latency recorder
    pub fn new() -> Self {
        Self {
            measurements: Vec::new(),
            writer: None,
            header_printed: false,
        }
    }

    /// Create a new latency recorder with file output
    pub fn with_output(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::create(path)?;
        let writer = BufWriter::new(file);
        
        Ok(Self {
            measurements: Vec::new(),
            writer: Some(writer),
            header_printed: false,
        })
    }

    /// Record a new latency measurement
    pub fn record(&mut self, duration: Duration) {
        self.measurements.push(duration);
    }

    /// Start a timer for measuring latency
    pub fn start_timer(&self) -> Instant {
        Instant::now()
    }

    /// Stop a timer and record the latency
    pub fn stop_timer(&mut self, start: Instant) {
        self.record(start.elapsed());
    }

    /// Calculate latency statistics and update the stats struct
    pub fn calculate_stats(&mut self, stats: &mut Stats) {
        if self.measurements.is_empty() {
            return;
        }

        // Sort measurements for percentile calculations
        self.measurements.sort_unstable();

        // Calculate min/max
        stats.latency_min = *self.measurements.first().unwrap_or(&Duration::ZERO);
        stats.latency_max = *self.measurements.last().unwrap_or(&Duration::ZERO);

        // Calculate average
        let total: Duration = self.measurements.iter().sum();
        stats.latency_avg = total / self.measurements.len() as u32;

        // Calculate percentiles
        let len = self.measurements.len();
        let p50_idx = (len as f64 * 0.50) as usize;
        let p95_idx = (len as f64 * 0.95) as usize;
        let p99_idx = (len as f64 * 0.99) as usize;

        stats.latency_p50 = self.measurements.get(p50_idx).copied().unwrap_or(Duration::ZERO);
        stats.latency_p95 = self.measurements.get(p95_idx).copied().unwrap_or(Duration::ZERO);
        stats.latency_p99 = self.measurements.get(p99_idx).copied().unwrap_or(Duration::ZERO);

        // Write to file if configured
        if let Some(writer) = &mut self.writer {
            if !self.header_printed {
                writeln!(
                    writer,
                    "Requests,MinLatency,MaxLatency,AvgLatency,P50Latency,P95Latency,P99Latency"
                ).expect("Failed to write header");
                
                self.header_printed = true;
            }
            
            writeln!(
                writer,
                "{},{},{},{},{},{},{}",
                stats.request_count,
                stats.latency_min.as_millis(),
                stats.latency_max.as_millis(),
                stats.latency_avg.as_millis(),
                stats.latency_p50.as_millis(),
                stats.latency_p95.as_millis(),
                stats.latency_p99.as_millis()
            ).expect("Failed to write latency stats");
        }
    }

    /// Reset all measurements
    pub fn reset(&mut self) {
        self.measurements.clear();
    }
}
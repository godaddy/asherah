use std::fmt;
use std::io;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::sync::mpsc;

pub mod cache2k;
pub mod wikipedia;
pub mod youtube;
pub mod storage;
pub mod zipf;
pub mod report;
pub mod files;
pub mod memory;
pub mod latency;

/// Provider trait for trace data sources
pub trait Provider: Send + Sync {
    /// Provide keys to the channel until exhausted or context is cancelled
    fn provide(&self, keys_tx: mpsc::Sender<Box<dyn KeyType>>);
}

/// KeyType trait for different types of keys
pub trait KeyType: Send + fmt::Debug {
    /// Get the string representation of the key
    fn to_string(&self) -> String;
}

impl KeyType for u32 {
    fn to_string(&self) -> String {
        self.to_string()
    }
}

impl KeyType for u64 {
    fn to_string(&self) -> String {
        self.to_string()
    }
}

impl KeyType for String {
    fn to_string(&self) -> String {
        self.clone()
    }
}

/// Stats struct for tracking performance metrics
#[derive(Debug, Default, Clone)]
pub struct Stats {
    pub request_count: u64,
    pub kms_op_count: u64,
    pub kms_encrypt_count: u64,
    pub kms_decrypt_count: u64,
    pub metastore_op_count: u64,
    pub metastore_load_count: u64,
    pub metastore_load_latest_count: u64,
    pub metastore_store_count: u64,
    pub op_rate: f64,
    pub latency_min: Duration,
    pub latency_max: Duration,
    pub latency_avg: Duration,
    pub latency_p50: Duration,
    pub latency_p95: Duration,
    pub latency_p99: Duration,
    pub memory_heap_mb: f64,
    pub memory_stack_mb: f64,
    pub memory_total_mb: f64,
}

/// Options for configuring the benchmark
#[derive(Debug, Clone)]
pub struct Options {
    pub policy: String,
    pub cache_size: usize,
    pub report_interval: usize,
    pub max_items: usize,
    pub track_latency: bool,
    pub track_memory: bool,
    pub output_dir: String,
}

/// Reporter trait for outputting benchmark results
pub trait Reporter {
    /// Report stats from a benchmark run
    fn report(&mut self, stats: &Stats, options: &Options);
    
    /// Report latency metrics
    fn report_latency(&mut self, stats: &Stats, options: &Options) {
        // Default implementation does nothing
    }
    
    /// Report memory usage metrics
    fn report_memory(&mut self, stats: &Stats, options: &Options) {
        // Default implementation does nothing
    }
}

/// File reader interface for supporting multiple file formats
pub trait FileReader: Send + Sync {
    /// Read from the file
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize>;
    
    /// Reset the file to the beginning
    fn reset(&mut self) -> io::Result<()>;
    
    /// Close the file
    fn close(&mut self) -> io::Result<()>;
}

/// Create a provider for different trace types
pub fn create_provider(provider_type: &str, reader: Arc<dyn FileReader>) -> Arc<dyn Provider> {
    match provider_type {
        "cache2k" => Arc::new(cache2k::Cache2kProvider::new(reader)),
        "wikipedia" => Arc::new(wikipedia::WikipediaProvider::new(reader)),
        "youtube" => Arc::new(youtube::YouTubeProvider::new(reader)),
        "storage" => Arc::new(storage::StorageProvider::new(reader)),
        _ => panic!("Unknown provider type: {}", provider_type),
    }
}

/// Available cache policies
pub const POLICIES: &[&str] = &[
    "session-legacy",
    "session-slru",
    "shared-slru",
    "shared-lru",
    "shared-tinylfu",
    "shared-lfu",
];
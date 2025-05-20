use std::fs::File;
use std::io::{self, Write};
use std::path::Path;
use std::sync::{Arc, atomic::{AtomicU64, Ordering}};
use std::time::{Duration, Instant};
use rand::Rng;
use tokio::sync::mpsc;

use crate::{Provider, KeyType, Reporter, Stats, Options};
use crate::latency::LatencyRecorder;
use crate::memory::MemoryTracker;

// Constants for benchmark configuration
pub const PRODUCT: &str = "enclibrary";
pub const SERVICE: &str = "asherah";
pub const STATIC_KEY: &str = "thisIsAStaticMasterKeyForTesting";
pub const PAYLOAD_SIZE_BYTES: usize = 100;

/// File-based reporter to write benchmark results to a CSV file
pub struct FileReporter {
    writer: io::BufWriter<File>,
    header_printed: bool,
    latency_reporter: Option<io::BufWriter<File>>,
    latency_header_printed: bool,
    memory_reporter: Option<io::BufWriter<File>>,
    memory_header_printed: bool,
}

impl FileReporter {
    /// Create a new file reporter
    pub fn new(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::create(path)?;
        let writer = io::BufWriter::new(file);
        
        Ok(Self {
            writer,
            header_printed: false,
            latency_reporter: None,
            latency_header_printed: false,
            memory_reporter: None,
            memory_header_printed: false,
        })
    }
    
    /// Configure latency reporting
    pub fn with_latency_output(mut self, path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::create(path)?;
        self.latency_reporter = Some(io::BufWriter::new(file));
        Ok(self)
    }
    
    /// Configure memory reporting
    pub fn with_memory_output(mut self, path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::create(path)?;
        self.memory_reporter = Some(io::BufWriter::new(file));
        Ok(self)
    }
}

impl Reporter for FileReporter {
    fn report(&mut self, stats: &Stats, options: &Options) {
        if !self.header_printed {
            writeln!(
                self.writer,
                "Requests,KMSOps,KMSEncrypts,KMSDecrypts,MetastoreOps,MetastoreLoads,MetastoreLoadLatests,MetastoreStores,OpRate,CacheSize"
            ).expect("Failed to write header");
            
            self.header_printed = true;
        }
        
        writeln!(
            self.writer,
            "{},{},{},{},{},{},{},{},{:.04},{},",
            stats.request_count,
            stats.kms_op_count,
            stats.kms_encrypt_count,
            stats.kms_decrypt_count,
            stats.metastore_op_count,
            stats.metastore_load_count,
            stats.metastore_load_latest_count,
            stats.metastore_store_count,
            stats.op_rate,
            options.cache_size
        ).expect("Failed to write stats");
    }
    
    fn report_latency(&mut self, stats: &Stats, _options: &Options) {
        if let Some(writer) = &mut self.latency_reporter {
            if !self.latency_header_printed {
                writeln!(
                    writer,
                    "Requests,MinLatency,MaxLatency,AvgLatency,P50Latency,P95Latency,P99Latency"
                ).expect("Failed to write latency header");
                
                self.latency_header_printed = true;
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
    
    fn report_memory(&mut self, stats: &Stats, _options: &Options) {
        if let Some(writer) = &mut self.memory_reporter {
            if !self.memory_header_printed {
                writeln!(
                    writer,
                    "Requests,HeapUsage,StackUsage,TotalUsage"
                ).expect("Failed to write memory header");
                
                self.memory_header_printed = true;
            }
            
            writeln!(
                writer,
                "{},{:.2},{:.2},{:.2}",
                stats.request_count,
                stats.memory_heap_mb,
                stats.memory_stack_mb,
                stats.memory_total_mb
            ).expect("Failed to write memory stats");
        }
    }
}

/// Counter for tracking operations
#[derive(Debug, Default)]
pub struct Counter {
    inner: AtomicU64,
}

impl Counter {
    /// Create a new counter
    pub fn new() -> Self {
        Self {
            inner: AtomicU64::new(0),
        }
    }
    
    /// Increment the counter by a value
    pub fn inc(&self, value: u64) {
        self.inner.fetch_add(value, Ordering::Relaxed);
    }
    
    /// Get the current count
    pub fn count(&self) -> u64 {
        self.inner.load(Ordering::Relaxed)
    }
}

/// Tracked KMS implementation that counts encrypt/decrypt operations
pub struct TrackedKMS {
    decrypt_counter: Counter,
    encrypt_counter: Counter,
}

impl TrackedKMS {
    /// Create a new tracked KMS
    pub fn new() -> Self {
        Self {
            decrypt_counter: Counter::new(),
            encrypt_counter: Counter::new(),
        }
    }
    
    /// Track a decrypt operation
    pub fn track_decrypt(&self) {
        self.decrypt_counter.inc(1);
    }
    
    /// Track an encrypt operation
    pub fn track_encrypt(&self) {
        self.encrypt_counter.inc(1);
    }
    
    /// Get the decrypt count
    pub fn decrypt_count(&self) -> u64 {
        self.decrypt_counter.count()
    }
    
    /// Get the encrypt count
    pub fn encrypt_count(&self) -> u64 {
        self.encrypt_counter.count()
    }
}

/// Delayed metastore implementation that counts operations
pub struct DelayedMetastore {
    load_counter: Counter,
    load_latest_counter: Counter,
    store_counter: Counter,
    delay: Duration,
    jitter: Duration,
}

impl DelayedMetastore {
    /// Create a new delayed metastore
    pub fn new(delay_ms: u64, jitter_ms: u64) -> Self {
        Self {
            load_counter: Counter::new(),
            load_latest_counter: Counter::new(),
            store_counter: Counter::new(),
            delay: Duration::from_millis(delay_ms),
            jitter: Duration::from_millis(jitter_ms),
        }
    }
    
    /// Simulate a delay with jitter
    pub fn delay_with_jitter(&self) {
        let mut jitter = Duration::from_millis(0);
        if !self.jitter.is_zero() {
            let rand_jitter = rand::thread_rng().gen_range(0..self.jitter.as_millis() as u64);
            jitter = Duration::from_millis(rand_jitter);
        }
        
        if !self.delay.is_zero() {
            let sleep_time = self.delay + jitter;
            let start = Instant::now();
            while start.elapsed() < sleep_time {
                // Busy wait to simulate work
                std::hint::spin_loop();
            }
        }
    }
    
    /// Track a load operation
    pub fn track_load(&self) {
        self.load_counter.inc(1);
        self.delay_with_jitter();
    }
    
    /// Track a load latest operation
    pub fn track_load_latest(&self) {
        self.load_latest_counter.inc(1);
        self.delay_with_jitter();
    }
    
    /// Track a store operation
    pub fn track_store(&self) {
        self.store_counter.inc(1);
        self.delay_with_jitter();
    }
    
    /// Get the load count
    pub fn load_count(&self) -> u64 {
        self.load_counter.count()
    }
    
    /// Get the load latest count
    pub fn load_latest_count(&self) -> u64 {
        self.load_latest_counter.count()
    }
    
    /// Get the store count
    pub fn store_count(&self) -> u64 {
        self.store_counter.count()
    }
}

/// Run a benchmark with a provider and reporter
pub async fn benchmark_session_factory(
    provider: Arc<dyn Provider>,
    reporter: &mut dyn Reporter,
    options: &Options,
    kms: Arc<TrackedKMS>,
    metastore: Arc<DelayedMetastore>,
) {
    // Initialize latency tracking if enabled
    let mut latency_recorder = if options.track_latency {
        let latency_path = format!("{}/latency_{}.txt", options.output_dir, options.policy.to_lowercase());
        Some(LatencyRecorder::with_output(latency_path).expect("Failed to create latency tracker"))
    } else {
        None
    };
    
    // Initialize memory tracking if enabled
    let mut memory_tracker = if options.track_memory {
        let memory_path = format!("{}/memory_{}.txt", options.output_dir, options.policy.to_lowercase());
        Some(MemoryTracker::with_output(memory_path).expect("Failed to create memory tracker"))
    } else {
        None
    };
    // Create a channel for keys
    let (keys_tx, mut keys_rx) = mpsc::channel(100);
    
    // Start providing keys
    provider.provide(keys_tx);
    
    let mut stats = Stats::default();
    let mut i = 0;
    
    // Generate random bytes for encryption
    let random_bytes = vec![0u8; PAYLOAD_SIZE_BYTES]; // In real implementation, use random data
    
    // Process keys
    while let Some(key) = keys_rx.recv().await {
        if options.max_items > 0 && i >= options.max_items {
            break;
        }
        
        // Simulate session factory and encryption
        let partition = format!("partition-{}", key.to_string());
        
        // Track latency if enabled
        let start_time = if latency_recorder.is_some() {
            Some(Instant::now())
        } else {
            None
        };
        
        // Simulate getting a session
        metastore.track_load_latest();
        kms.track_decrypt();
        
        // Simulate encryption
        metastore.track_store();
        kms.track_encrypt();
        
        // Record latency if tracking is enabled
        if let Some(recorder) = &mut latency_recorder {
            if let Some(start) = start_time {
                recorder.stop_timer(start);
            }
        }
        
        // Sample memory usage if tracking is enabled
        if let Some(tracker) = &mut memory_tracker {
            tracker.sample(&mut stats);
        }
        
        i += 1;
        if options.report_interval > 0 && i % options.report_interval == 0 {
            update_stats(&mut stats, &metastore, &kms, i as u64);
            
            // Update latency stats if tracking is enabled
            if let Some(recorder) = &mut latency_recorder {
                recorder.calculate_stats(&mut stats);
            }
            
            reporter.report(&stats, options);
            
            // Report specialized metrics
            if options.track_latency {
                reporter.report_latency(&stats, options);
            }
            
            if options.track_memory {
                reporter.report_memory(&stats, options);
            }
        }
    }
    
    // Final report if we haven't been reporting periodically
    if options.report_interval == 0 || i % options.report_interval != 0 {
        update_stats(&mut stats, &metastore, &kms, i as u64);
        
        // Update latency stats if tracking is enabled
        if let Some(recorder) = &mut latency_recorder {
            recorder.calculate_stats(&mut stats);
        }
        
        reporter.report(&stats, options);
        
        // Report specialized metrics
        if options.track_latency {
            reporter.report_latency(&stats, options);
        }
        
        if options.track_memory {
            reporter.report_memory(&stats, options);
        }
    }
}

/// Update the stats struct with current counters
fn update_stats(stats: &mut Stats, metastore: &DelayedMetastore, kms: &TrackedKMS, requests: u64) {
    stats.request_count = requests;
    
    stats.metastore_load_count = metastore.load_count();
    stats.metastore_load_latest_count = metastore.load_latest_count();
    stats.metastore_store_count = metastore.store_count();
    stats.metastore_op_count = stats.metastore_load_count + stats.metastore_load_latest_count + stats.metastore_store_count;
    
    stats.kms_decrypt_count = kms.decrypt_count();
    stats.kms_encrypt_count = kms.encrypt_count();
    stats.kms_op_count = stats.kms_decrypt_count + stats.kms_encrypt_count;
    
    stats.op_rate = (stats.metastore_op_count + stats.kms_op_count) as f64 / stats.request_count as f64;
}
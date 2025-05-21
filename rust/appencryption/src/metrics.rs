//! Metrics module for the application encryption library
//!
//! This module provides a metrics interface for collecting and reporting performance metrics.
//! By default, metrics are disabled and use a no-op implementation.

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::RwLock;
use std::time::{Duration, Instant};

// Global flag to check if metrics are enabled
static METRICS_ENABLED: AtomicBool = AtomicBool::new(false);

/// Metrics provider interface
pub trait MetricsProvider: Send + Sync {
    /// Records a counter increment
    fn increment_counter(&self, name: &str, value: u64);

    /// Records a gauge value
    fn record_gauge(&self, name: &str, value: f64);

    /// Records a timer duration
    fn record_timer(&self, name: &str, duration: Duration);

    /// Registers a counter
    fn register_counter(&self, name: &str);

    /// Registers a gauge
    fn register_gauge(&self, name: &str);

    /// Registers a timer
    fn register_timer(&self, name: &str);
}

/// A no-op metrics provider that discards all metrics
#[derive(Debug)]
pub struct NoopMetricsProvider;

impl Default for NoopMetricsProvider {
    fn default() -> Self {
        Self
    }
}

impl NoopMetricsProvider {
    /// Create a new no-op metrics provider
    pub fn new() -> Self {
        Self
    }

    /// Create a boxed instance ready for use with set_metrics_provider
    pub fn boxed() -> Box<dyn MetricsProvider> {
        Box::new(Self::new())
    }
}

impl MetricsProvider for NoopMetricsProvider {
    fn increment_counter(&self, _name: &str, _value: u64) {}
    fn record_gauge(&self, _name: &str, _value: f64) {}
    fn record_timer(&self, _name: &str, _duration: Duration) {}
    fn register_counter(&self, _name: &str) {}
    fn register_gauge(&self, _name: &str) {}
    fn register_timer(&self, _name: &str) {}
}

// Global metrics provider
static METRICS_PROVIDER: RwLock<Option<Box<dyn MetricsProvider>>> = RwLock::new(None);

/// Set the metrics provider for the application encryption library
pub fn set_metrics_provider(provider: Box<dyn MetricsProvider>) {
    let mut global_provider = METRICS_PROVIDER.write().unwrap();
    *global_provider = Some(provider);
    METRICS_ENABLED.store(true, Ordering::SeqCst);
}

/// Disable metrics collection
pub fn disable_metrics() {
    let mut global_provider = METRICS_PROVIDER.write().unwrap();
    *global_provider = None;
    METRICS_ENABLED.store(false, Ordering::SeqCst);
}

/// Check if metrics are enabled
pub fn metrics_enabled() -> bool {
    METRICS_ENABLED.load(Ordering::SeqCst)
}

/// Register a counter metric
pub fn register_counter(name: &str) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.register_counter(name);
        }
    }
}

/// Register a gauge metric
pub fn register_gauge(name: &str) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.register_gauge(name);
        }
    }
}

/// Register a timer metric
pub fn register_timer(name: &str) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.register_timer(name);
        }
    }
}

/// Increment a counter metric
pub fn increment_counter(name: &str, value: u64) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.increment_counter(name, value);
        }
    }
}

/// Record a gauge metric
pub fn record_gauge(name: &str, value: f64) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.record_gauge(name, value);
        }
    }
}

/// Record a timer metric
pub fn record_timer(name: &str, duration: Duration) {
    if metrics_enabled() {
        if let Some(provider) = METRICS_PROVIDER.read().unwrap().as_ref() {
            provider.record_timer(name, duration);
        }
    }
}

/// Timer for measuring and recording operation duration
#[derive(Debug)]
pub struct Timer {
    /// Name of the timer metric
    name: String,

    /// Start time of the operation
    start: Instant,
}

impl Timer {
    /// Create a new timer with the given name
    pub fn new(name: impl Into<String>) -> Self {
        let name = name.into();
        register_timer(&name);

        Self {
            name,
            start: Instant::now(),
        }
    }

    /// Record the elapsed time
    pub fn observe_duration(&self) {
        record_timer(&self.name, self.start.elapsed());
    }
}

impl Drop for Timer {
    fn drop(&mut self) {
        self.observe_duration();
    }
}

/// Macro for creating a timer
#[macro_export]
macro_rules! timer {
    ($name:expr) => {
        {
            if $crate::metrics::metrics_enabled() {
                Some($crate::metrics::Timer::new($name))
            } else {
                None
            }
        }
    };
    ($name:expr, $($key:expr => $value:expr),+) => {
        {
            let mut full_name = $name.to_string();
            $(
                full_name.push_str(&format!(".{}={}", $key, $value));
            )+
            if $crate::metrics::metrics_enabled() {
                Some($crate::metrics::Timer::new(full_name))
            } else {
                None
            }
        }
    };
}

/// Macro for incrementing a counter
#[macro_export]
macro_rules! counter {
    ($name:expr) => {{
        $crate::metrics::register_counter($name);
        CounterHelper::new($name)
    }};
}

/// Helper struct for counter operations
#[derive(Debug)]
pub struct CounterHelper {
    /// Name of the counter metric
    name: String,
}

impl CounterHelper {
    /// Create a new counter helper
    pub fn new(name: impl Into<String>) -> Self {
        let name = name.into();
        register_counter(&name);

        Self { name }
    }

    /// Increment the counter by the given value
    pub fn increment(&self, value: u64) {
        increment_counter(&self.name, value);
    }
}

/// Prometheus metrics provider implementation
pub mod prometheus {
    use super::*;
    use std::collections::HashMap;
    use std::sync::Mutex;

    /// A simple prometheus metrics provider
    #[derive(Debug)]
    pub struct PrometheusMetricsProvider {
        /// Registered counters
        counters: Mutex<HashMap<String, ()>>,

        /// Registered gauges
        gauges: Mutex<HashMap<String, ()>>,

        /// Registered timers
        timers: Mutex<HashMap<String, ()>>,
    }

    impl Default for PrometheusMetricsProvider {
        fn default() -> Self {
            Self {
                counters: Mutex::new(HashMap::new()),
                gauges: Mutex::new(HashMap::new()),
                timers: Mutex::new(HashMap::new()),
            }
        }
    }

    impl PrometheusMetricsProvider {
        /// Create a new prometheus metrics provider
        pub fn new() -> Self {
            Self::default()
        }

        /// Create a boxed instance ready for use with set_metrics_provider
        pub fn boxed() -> Box<dyn MetricsProvider> {
            Box::new(Self::new())
        }
    }

    impl MetricsProvider for PrometheusMetricsProvider {
        fn increment_counter(&self, name: &str, value: u64) {
            // In a real implementation, this would use prometheus client library
            // For now, we just log the metric
            log::debug!("METRIC counter: {} = {}", name, value);
        }

        fn record_gauge(&self, name: &str, value: f64) {
            // In a real implementation, this would use prometheus client library
            // For now, we just log the metric
            log::debug!("METRIC gauge: {} = {}", name, value);
        }

        fn record_timer(&self, name: &str, duration: Duration) {
            // In a real implementation, this would use prometheus client library
            // For now, we just log the metric
            log::debug!("METRIC timer: {} = {:?}", name, duration);
        }

        fn register_counter(&self, name: &str) {
            let mut counters = self.counters.lock().unwrap();
            counters.insert(name.to_string(), ());
        }

        fn register_gauge(&self, name: &str) {
            let mut gauges = self.gauges.lock().unwrap();
            gauges.insert(name.to_string(), ());
        }

        fn register_timer(&self, name: &str) {
            let mut timers = self.timers.lock().unwrap();
            timers.insert(name.to_string(), ());
        }
    }
}

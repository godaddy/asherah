//! Logging module for the application encryption library
//!
//! This module provides a simple logging interface with a focus on debug logging.
//! By default, logging is disabled and uses a no-op implementation.

use std::fmt;
use std::sync::RwLock;

/// Logger interface for the application encryption library
pub trait Logger: Send + Sync {
    /// Log a debug message with formatting
    fn debug(&self, message: &str);

    /// Log a debug message with formatting
    fn debugf(&self, fmt: fmt::Arguments<'_>);
}

/// A no-op logger that does nothing
#[derive(Debug)]
pub struct NoopLogger;

impl Default for NoopLogger {
    fn default() -> Self {
        Self
    }
}

impl NoopLogger {
    /// Create a new no-op logger
    pub fn new() -> Self {
        Self
    }

    /// Create a boxed instance
    pub fn boxed() -> Box<dyn Logger> {
        Box::new(Self::new())
    }
}

impl Logger for NoopLogger {
    fn debug(&self, _message: &str) {}
    fn debugf(&self, _fmt: fmt::Arguments<'_>) {}
}

// Global logger (default to noop)
static LOGGER: RwLock<Option<Box<dyn Logger>>> = RwLock::new(None);

/// Set the logger for the application encryption library
pub fn set_logger(logger: Box<dyn Logger>) {
    let mut global_logger = LOGGER.write().unwrap();
    *global_logger = Some(logger);
}

/// Check if debug logging is enabled
pub fn debug_enabled() -> bool {
    let global_logger = LOGGER.read().unwrap();
    global_logger.is_some()
}

/// Log a debug message
pub fn debug(message: &str) {
    let global_logger = LOGGER.read().unwrap();
    if let Some(logger) = global_logger.as_ref() {
        logger.debug(message);
    }
}

/// Log a formatted debug message
#[macro_export]
macro_rules! debugf {
    ($($arg:tt)*) => {{
        let global_logger = $crate::log::LOGGER.read().unwrap();
        if let Some(logger) = global_logger.as_ref() {
            logger.debugf(format_args!($($arg)*));
        }
    }};
}

/// Provides a simple logger that writes to standard output
#[derive(Debug)]
pub struct StdoutLogger;

impl Default for StdoutLogger {
    fn default() -> Self {
        Self
    }
}

impl StdoutLogger {
    /// Create a new stdout logger
    pub fn new() -> Self {
        Self
    }

    /// Create a boxed instance
    pub fn boxed() -> Box<dyn Logger> {
        Box::new(Self::new())
    }
}

impl Logger for StdoutLogger {
    fn debug(&self, message: &str) {
        println!("[DEBUG] {}", message);
    }

    fn debugf(&self, fmt: fmt::Arguments<'_>) {
        println!("[DEBUG] {}", fmt);
    }
}

/// Helper struct to enable logging within a scope
pub struct LoggingGuard {
    previous_logger: Option<Box<dyn Logger>>,
}

impl std::fmt::Debug for LoggingGuard {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("LoggingGuard")
            .field("has_previous_logger", &self.previous_logger.is_some())
            .finish()
    }
}

impl LoggingGuard {
    /// Create a new logging guard with the given logger
    pub fn new(logger: Box<dyn Logger>) -> Self {
        let previous_logger = {
            let mut global_logger = LOGGER.write().unwrap();
            std::mem::replace(&mut *global_logger, Some(logger))
        };

        Self { previous_logger }
    }
}

impl Drop for LoggingGuard {
    fn drop(&mut self) {
        let mut global_logger = LOGGER.write().unwrap();
        *global_logger = self.previous_logger.take();
    }
}

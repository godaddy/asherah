//! Cache implementations with various eviction policies
//!
//! This module provides a generic cache implementation with multiple eviction policies:
//! - LRU (Least Recently Used)
//! - LFU (Least Frequently Used)
//! - TLFU (Tiny Least Frequently Used)
//! - SLRU (Segmented Least Recently Used)

mod lfu;
mod lru;
pub mod simple;
mod slru;
mod tlfu;

use std::fmt::{Debug, Display};
use std::hash::Hash;
use std::sync::Arc;
use std::time::Duration;

pub use lfu::LfuCache;
pub use lru::LruCache;
pub use simple::SimpleCache;
pub use slru::SlruCache;
pub use tlfu::TlfuCache;

/// Cache policy types
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CachePolicy {
    /// Least Recently Used
    LRU,
    /// Least Frequently Used
    LFU,
    /// Tiny Least Frequently Used (admission controlled LFU)
    TLFU,
    /// Segmented Least Recently Used
    SLRU,
    /// Simple (no eviction)
    Simple,
}

impl Display for CachePolicy {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            CachePolicy::LRU => write!(f, "lru"),
            CachePolicy::LFU => write!(f, "lfu"),
            CachePolicy::TLFU => write!(f, "tlfu"),
            CachePolicy::SLRU => write!(f, "slru"),
            CachePolicy::Simple => write!(f, "simple"),
        }
    }
}

/// A callback function called when an item is evicted from the cache
pub type EvictCallback<K, V> = Arc<dyn Fn(&K, &V) + Send + Sync>;

/// Cache interface for different implementations
pub trait Cache<K, V>: Send + Sync {
    /// Get a value from the cache
    fn get(&self, key: &K) -> Option<Arc<V>>;

    /// Insert a value into the cache
    fn insert(&self, key: K, value: V) -> bool;

    /// Remove a value from the cache
    fn remove(&self, key: &K) -> bool;

    /// Return the number of items in the cache
    fn len(&self) -> usize;

    /// Return true if the cache is empty
    fn is_empty(&self) -> bool {
        self.len() == 0
    }

    /// Return the capacity of the cache
    fn capacity(&self) -> usize;

    /// Clear all items from the cache
    fn clear(&self);

    /// Close the cache and clean up resources
    fn close(&self);
}

/// Builder for creating cache instances
pub struct CacheBuilder<K, V> {
    capacity: usize,
    policy: CachePolicy,
    evict_callback: Option<EvictCallback<K, V>>,
    ttl: Option<Duration>,
}

impl<K, V> CacheBuilder<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    /// Create a new cache builder with the given capacity
    pub fn new(capacity: usize) -> Self {
        Self {
            capacity,
            policy: CachePolicy::LRU,
            evict_callback: None,
            ttl: None,
        }
    }

    /// Set the cache policy
    pub fn with_policy(mut self, policy: CachePolicy) -> Self {
        self.policy = policy;
        self
    }

    /// Set the eviction callback
    pub fn with_evict_callback<F>(mut self, callback: F) -> Self
    where
        F: Fn(&K, &V) + Send + Sync + 'static,
    {
        self.evict_callback = Some(Arc::new(callback));
        self
    }

    /// Set the time-to-live for cache entries
    pub fn with_ttl(mut self, ttl: Duration) -> Self {
        self.ttl = Some(ttl);
        self
    }

    /// Build the cache with the configured options
    pub fn build(self) -> Arc<dyn Cache<K, V>> {
        match self.policy {
            CachePolicy::LRU => {
                let cache = LruCache::new(self.capacity, self.evict_callback, self.ttl);
                Arc::new(cache)
            }
            CachePolicy::LFU => {
                let cache = LfuCache::new(self.capacity, self.evict_callback, self.ttl);
                Arc::new(cache)
            }
            CachePolicy::TLFU => {
                let cache = TlfuCache::new(self.capacity, self.evict_callback, self.ttl);
                Arc::new(cache)
            }
            CachePolicy::SLRU => {
                let cache = SlruCache::new(self.capacity, self.evict_callback, self.ttl);
                Arc::new(cache)
            }
            CachePolicy::Simple => {
                let cache = SimpleCache::new(self.capacity, self.evict_callback, self.ttl);
                Arc::new(cache)
            }
        }
    }
}

/// No-op eviction callback
pub fn noop_evict_callback<K, V>(_key: &K, _value: &V) {
    // Do nothing
}

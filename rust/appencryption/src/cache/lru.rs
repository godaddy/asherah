use std::collections::{HashMap, VecDeque};
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

/// LRU (Least Recently Used) cache implementation
pub struct LruCache<K, V> {
    /// Current entries in the cache
    entries: RwLock<HashMap<K, Arc<V>>>,

    /// LRU queue to track access order
    lru_queue: RwLock<VecDeque<K>>,

    /// Entry metadata for tracking access times
    metadata: RwLock<HashMap<K, Instant>>,

    /// Maximum number of entries in the cache
    capacity: usize,

    /// Optional callback when an item is evicted
    evict_callback: Option<EvictCallback<K, V>>,

    /// Optional time-to-live for entries
    ttl: Option<Duration>,
}

impl<K, V> LruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    /// Create a new LRU cache with the given capacity
    pub fn new(
        capacity: usize,
        evict_callback: Option<EvictCallback<K, V>>,
        ttl: Option<Duration>,
    ) -> Self {
        Self {
            entries: RwLock::new(HashMap::with_capacity(capacity)),
            lru_queue: RwLock::new(VecDeque::with_capacity(capacity)),
            metadata: RwLock::new(HashMap::with_capacity(capacity)),
            capacity,
            evict_callback,
            ttl,
        }
    }

    /// Check if an entry has expired
    fn is_expired(&self, key: &K) -> bool {
        if let Some(ttl) = self.ttl {
            if let Some(last_accessed) = self.metadata.read().unwrap().get(key) {
                return last_accessed.elapsed() > ttl;
            }
        }
        false
    }

    /// Evict the least recently used item from the cache
    fn evict_lru(&self) -> bool {
        let mut lru_queue = self.lru_queue.write().unwrap();
        if let Some(key) = lru_queue.pop_back() {
            let mut entries = self.entries.write().unwrap();
            let mut metadata = self.metadata.write().unwrap();

            if let Some(value) = entries.remove(&key) {
                metadata.remove(&key);

                // Call eviction callback if provided
                if let Some(callback) = &self.evict_callback {
                    callback(&key, &value);
                }

                return true;
            }
        }
        false
    }

    /// Update the LRU status for a key (move to front of queue)
    fn update_lru(&self, key: &K) {
        let mut lru_queue = self.lru_queue.write().unwrap();

        // Remove the key from its current position
        if let Some(pos) = lru_queue.iter().position(|k| k == key) {
            lru_queue.remove(pos);
        }

        // Add the key to the front of the queue
        lru_queue.push_front(key.clone());

        // Update last accessed time
        let mut metadata = self.metadata.write().unwrap();
        metadata.insert(key.clone(), Instant::now());
    }
}

impl<K, V> Cache<K, V> for LruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        // Check if key exists and hasn't expired
        if self.is_expired(key) {
            self.remove(key);
            return None;
        }

        let entries = self.entries.read().unwrap();
        if let Some(value) = entries.get(key) {
            // Update LRU status
            self.update_lru(key);
            return Some(value.clone());
        }

        None
    }

    fn insert(&self, key: K, value: V) -> bool {
        // If at capacity, evict the least recently used item
        let current_len = {
            let entries = self.entries.read().unwrap();
            entries.len()
        };

        if current_len >= self.capacity {
            self.evict_lru();
        }

        // Insert new value
        let arc_value = Arc::new(value);
        let mut entries = self.entries.write().unwrap();
        entries.insert(key.clone(), arc_value);

        // Update LRU status
        self.update_lru(&key);

        true
    }

    fn remove(&self, key: &K) -> bool {
        let mut entries = self.entries.write().unwrap();
        if let Some(value) = entries.remove(key) {
            // Update LRU queue
            let mut lru_queue = self.lru_queue.write().unwrap();
            if let Some(pos) = lru_queue.iter().position(|k| k == key) {
                lru_queue.remove(pos);
            }

            // Update metadata
            let mut metadata = self.metadata.write().unwrap();
            metadata.remove(key);

            // Call eviction callback if provided
            if let Some(callback) = &self.evict_callback {
                callback(key, &value);
            }

            return true;
        }

        false
    }

    fn len(&self) -> usize {
        let entries = self.entries.read().unwrap();
        entries.len()
    }

    fn capacity(&self) -> usize {
        self.capacity
    }

    fn clear(&self) {
        let mut entries = self.entries.write().unwrap();
        let mut lru_queue = self.lru_queue.write().unwrap();
        let mut metadata = self.metadata.write().unwrap();

        // Call eviction callback for all entries if provided
        if let Some(callback) = &self.evict_callback {
            for (key, value) in entries.iter() {
                callback(key, value);
            }
        }

        entries.clear();
        lru_queue.clear();
        metadata.clear();
    }

    fn close(&self) {
        self.clear();
    }
}

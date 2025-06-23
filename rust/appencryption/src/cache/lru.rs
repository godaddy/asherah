use std::collections::{HashMap, VecDeque};
use std::fmt;
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

/// LRU (Least Recently Used) cache implementation
pub struct LruCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
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

impl<K, V> fmt::Debug for LruCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let entries_len = self
            .entries
            .read()
            .map(|entries| entries.len())
            .unwrap_or(0);

        f.debug_struct("LruCache")
            .field("capacity", &self.capacity)
            .field("ttl", &self.ttl)
            .field("entries_len", &entries_len)
            .finish()
    }
}

impl<K, V> LruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
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
            if let Some(last_accessed) = self
                .metadata
                .read()
                .expect("Failed to acquire read lock for metadata")
                .get(key)
            {
                return last_accessed.elapsed() > ttl;
            }
        }
        false
    }

    /// Evict the least recently used item from the cache
    fn evict_lru(&self) -> bool {
        let mut lru_queue = self
            .lru_queue
            .write()
            .expect("Failed to acquire write lock for LRU queue");
        if let Some(key) = lru_queue.pop_back() {
            let mut entries = self
                .entries
                .write()
                .expect("Failed to acquire write lock for entries");
            let mut metadata = self
                .metadata
                .write()
                .expect("Failed to acquire write lock for metadata");

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
        let mut lru_queue = self
            .lru_queue
            .write()
            .expect("Failed to acquire write lock for LRU queue");

        // Remove the key from its current position
        if let Some(pos) = lru_queue.iter().position(|k| k == key) {
            lru_queue.remove(pos);
        }

        // Add the key to the front of the queue
        lru_queue.push_front(key.clone());

        // Update last accessed time
        let mut metadata = self
            .metadata
            .write()
            .expect("Failed to acquire write lock for metadata");
        metadata.insert(key.clone(), Instant::now());
    }
}

impl<K, V> Cache<K, V> for LruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        // Check if key exists and hasn't expired
        if self.is_expired(key) {
            self.remove(key);
            return None;
        }

        let entries = self.entries.read().ok()?;
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
            let entries = self
                .entries
                .read()
                .expect("Failed to acquire read lock in LruCache::get");
            entries.len()
        };

        if current_len >= self.capacity {
            self.evict_lru();
        }

        // Insert new value
        let arc_value = Arc::new(value);
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        entries.insert(key.clone(), arc_value);

        // Update LRU status
        self.update_lru(&key);

        true
    }

    fn remove(&self, key: &K) -> bool {
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        if let Some(value) = entries.remove(key) {
            // Update LRU queue
            let mut lru_queue = self
                .lru_queue
                .write()
                .expect("Failed to acquire write lock for LRU queue");
            if let Some(pos) = lru_queue.iter().position(|k| k == key) {
                lru_queue.remove(pos);
            }

            // Update metadata
            let mut metadata = self
                .metadata
                .write()
                .expect("Failed to acquire write lock for metadata");
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
        let entries = self
            .entries
            .read()
            .expect("Failed to acquire read lock in LruCache::get");
        entries.len()
    }

    fn capacity(&self) -> usize {
        self.capacity
    }

    fn clear(&self) {
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        let mut lru_queue = self
            .lru_queue
            .write()
            .expect("Failed to acquire write lock for LRU queue");
        let mut metadata = self
            .metadata
            .write()
            .expect("Failed to acquire write lock for metadata");

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
